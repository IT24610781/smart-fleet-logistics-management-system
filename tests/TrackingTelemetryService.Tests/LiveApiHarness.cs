using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json.Nodes;

namespace TrackingTelemetryService.Tests;

/// <summary>
/// Shared plumbing for the live API tests (SCRUM-153). These call the REAL services in docker compose,
/// so they only run when you opt in:
///   set QA_RUN_LIVE_API=1
///   set "QA_ADMIN_PASSWORD=..."            (QA_ADMIN_USERNAME defaults to admin)
/// URLs default to 127.0.0.1 (avoids the Docker Desktop "localhost" dropped-connection problem on Windows):
///   QA_AUTH_URL 5100, QA_FLEET_URL 5041, QA_TRIP_URL 5051, QA_TRACKING_URL 5000.
/// Without QA_RUN_LIVE_API=1 every [LiveApiFact] test is reported as Skipped, so normal runs and CI are unaffected.
/// </summary>
public sealed class LiveApiFactAttribute : FactAttribute
{
    public LiveApiFactAttribute()
    {
        if (Environment.GetEnvironmentVariable("QA_RUN_LIVE_API") != "1")
        {
            Skip = "Live API test: set QA_RUN_LIVE_API=1 and QA_ADMIN_PASSWORD, with docker compose running.";
        }
    }
}

public sealed record ApiResult(HttpStatusCode Status, string Body)
{
    public JsonNode Json => string.IsNullOrWhiteSpace(Body) ? new JsonObject() : JsonNode.Parse(Body)!;
}

public sealed record LiveDriver(string Id, string Username, string Token);

public sealed record LiveTrip(string TripId, string VehicleId, LiveDriver Driver, string DispatcherToken);

public static class LiveApi
{
    public static readonly string AuthUrl = Env("QA_AUTH_URL", "http://127.0.0.1:5100");
    public static readonly string FleetUrl = Env("QA_FLEET_URL", "http://127.0.0.1:5041");
    public static readonly string TripUrl = Env("QA_TRIP_URL", "http://127.0.0.1:5051");
    public static readonly string TrackingUrl = Env("QA_TRACKING_URL", "http://127.0.0.1:5000");

    private static string Env(string name, string fallback) => Environment.GetEnvironmentVariable(name) ?? fallback;

    private static string AdminUsername => Env("QA_ADMIN_USERNAME", "admin");

    private static string AdminPassword => Environment.GetEnvironmentVariable("QA_ADMIN_PASSWORD")
        ?? throw new InvalidOperationException("Set QA_ADMIN_PASSWORD before running live API tests.");

    // ------------------------------------------------------------------ HTTP

    /// <summary>Sends one request. Connection-level failures are retried (Docker Desktop on Windows drops some connections);
    /// HTTP error statuses are NOT retried, they are returned for the test to assert on.</summary>
    public static async Task<ApiResult> SendAsync(
        string host, string path, HttpMethod method, object? body = null, string? token = null, string? rawJson = null)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                using var client = new HttpClient { BaseAddress = new Uri(host), Timeout = TimeSpan.FromSeconds(30) };
                using var request = new HttpRequestMessage(method, path);
                if (token != null) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                if (rawJson != null) request.Content = new StringContent(rawJson, Encoding.UTF8, "application/json");
                else if (body != null) request.Content = JsonContent.Create(body);

                using var response = await client.SendAsync(request);
                return new ApiResult(response.StatusCode, await response.Content.ReadAsStringAsync());
            }
            catch (HttpRequestException) when (attempt < 3)
            {
                await Task.Delay(1000 * attempt);
            }
        }
    }

    public static void Expect(ApiResult result, HttpStatusCode expected) =>
        Assert.True(result.Status == expected, $"Expected {(int)expected} but got {(int)result.Status}: {result.Body}");

    public static void ExpectSuccess(ApiResult result) =>
        Assert.True((int)result.Status is >= 200 and < 300, $"Setup call failed with {(int)result.Status}: {result.Body}");

    // ------------------------------------------------------------- accounts

    private static object NewUser(string username, string password) => new
    {
        username,
        email = username + "@example.com",
        password,
        confirmPassword = password,
        firstName = "QA",
        lastName = "Live",
        contactNumber = "0771234567"
    };

    public static async Task<string> LoginAsync(string username, string password)
    {
        var result = await SendAsync(AuthUrl, "/api/auth/login", HttpMethod.Post, new { username, password });
        ExpectSuccess(result);
        return result.Json["token"]!.GetValue<string>();
    }

    public static Task<string> AdminTokenAsync() => LoginAsync(AdminUsername, AdminPassword);

    public static async Task<LiveDriver> CreateDriverAsync(string adminToken)
    {
        var username = "qald" + Guid.NewGuid().ToString("N")[..8];
        var password = "QaLive!" + Guid.NewGuid().ToString("N");
        var created = await SendAsync(AuthUrl, "/api/users/drivers", HttpMethod.Post, NewUser(username, password), adminToken);
        ExpectSuccess(created);
        var token = await LoginAsync(username, password);
        return new LiveDriver(created.Json["id"]!.GetValue<string>(), username, token);
    }

    public static async Task<string> CreateDispatcherTokenAsync()
    {
        var username = "qalp" + Guid.NewGuid().ToString("N")[..8];
        var password = "QaLive!" + Guid.NewGuid().ToString("N");
        ExpectSuccess(await SendAsync(AuthUrl, "/api/auth/register/dispatcher", HttpMethod.Post, NewUser(username, password)));
        return await LoginAsync(username, password);
    }

    // ---------------------------------------------------------------- trips

    /// <summary>Creates a driver, dispatcher, vehicle and trip through the real APIs.
    /// With accepted = true the driver accepts the trip, so it is In-Progress; with false it stays Assigned.</summary>
    public static async Task<LiveTrip> StartTripAsync(bool accepted = true)
    {
        var admin = await AdminTokenAsync();
        var driver = await CreateDriverAsync(admin);
        var dispatcherToken = await CreateDispatcherTokenAsync();

        var suffix = Guid.NewGuid().ToString("N")[..8];
        var capacity = Random.Shared.Next(600000, 900000) + " persons";

        var vehicle = await SendAsync(FleetUrl, "/api/vehicles", HttpMethod.Post,
            new { registrationNo = "QL" + suffix.ToUpperInvariant(), type = "Van", capacity }, admin);
        ExpectSuccess(vehicle);
        var vehicleId = vehicle.Json["id"]!.GetValue<string>();

        var trip = await SendAsync(TripUrl, "/api/trips", HttpMethod.Post, new
        {
            pickupLocation = "QA Live " + suffix,
            dropoffLocation = "QA Live Destination",
            requiredCapacity = capacity
        }, dispatcherToken);
        ExpectSuccess(trip);
        var tripId = trip.Json["id"]!.GetValue<string>();

        ExpectSuccess(await SendAsync(TripUrl, $"/api/trips/{tripId}/assign", HttpMethod.Post,
            new { vehicleId, driverId = driver.Id }, dispatcherToken));

        if (accepted)
        {
            ExpectSuccess(await SendAsync(TripUrl, $"/api/trips/{tripId}/respond", HttpMethod.Post,
                new { decision = "accept" }, driver.Token));
        }

        return new LiveTrip(tripId, vehicleId, driver, dispatcherToken);
    }

    // ------------------------------------------------------------- tracking

    public static Task<ApiResult> PostPingAsync(
        string tripId, string? token, double latitude, double longitude, DateTime observedAtUtc) =>
        SendAsync(TrackingUrl, "/api/tracking/locations", HttpMethod.Post,
            new { tripId, latitude, longitude, observedAt = observedAtUtc }, token);

    public static Task<ApiResult> GetHistoryAsync(string tripId, string? token, string query = "") =>
        SendAsync(TrackingUrl, $"/api/tracking/trips/{tripId}/locations{query}", HttpMethod.Get, token: token);
}
