using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using OpenQA.Selenium;
using Xunit;

namespace Frontend.SeleniumTests;

/// <summary>
/// SCRUM-154 / US-15: Dispatcher views the live fleet map (/dispatcher/tracking).
///
/// Live and opt-in, exactly like SprintDispatchWorkflowTests:
///   QA_RUN_SPRINT3_UI=1   (reuses Sprint3BrowserFact)
///   QA_ADMIN_PASSWORD     (required), QA_ADMIN_USERNAME (default "admin")
///   QA_FRONTEND_URL       (default http://localhost:5173)
///   QA_AUTH_URL  http://localhost:5100   QA_FLEET_URL http://localhost:5041
///   QA_TRIP_URL  http://localhost:5051   QA_TRACKING_URL http://localhost:5000
///
/// The frontend must be started with VITE_TRACKING_API_URL (and the other
/// VITE_*_URL values) pointing at the docker-compose services; see
/// tests/qa-sprint4/README.md. Every test builds its own driver, vehicle and
/// In-Progress trip, so tests do not depend on each other or on seeded data.
/// </summary>
/// <summary>
/// Own collection with no shared fixture: these tests build all of their data through the APIs, so they must
/// not depend on the legacy SeededDriverFixture (which reads appsettings.Development.json and mints its own token).
/// </summary>
[CollectionDefinition("Sprint4 live")]
public class Sprint4LiveCollection { }

[Collection("Sprint4 live")]
public class TrackingPageTests : Sprint3SeleniumTestBase
{
    private static readonly string AuthUrl = Environment.GetEnvironmentVariable("QA_AUTH_URL") ?? "http://localhost:5100";
    private static readonly string FleetUrl = Environment.GetEnvironmentVariable("QA_FLEET_URL") ?? "http://localhost:5041";
    private static readonly string TripUrl = Environment.GetEnvironmentVariable("QA_TRIP_URL") ?? "http://localhost:5051";
    private static readonly string TrackingUrl = Environment.GetEnvironmentVariable("QA_TRACKING_URL") ?? "http://localhost:5000";
    private static readonly string AdminUsername = Environment.GetEnvironmentVariable("QA_ADMIN_USERNAME") ?? "admin";
    private static string AdminPassword => Environment.GetEnvironmentVariable("QA_ADMIN_PASSWORD")
        ?? throw new InvalidOperationException("Set QA_ADMIN_PASSWORD before running live QA.");

    private static readonly string ShotDir = Path.Combine(
        Environment.GetEnvironmentVariable("QA_OUTPUT_ROOT") ?? Path.Combine("TestResults", "QA-Sprint04"),
        "ui-screenshots");

    private readonly string pw = "QaTrack!" + Guid.NewGuid().ToString("N");

    private sealed record Scenario(
        string TripId, string Pickup, string VehicleReg,
        string DriverUser, string DriverToken, string DispatcherUser);

    // ---------------------------------------------------------------- API helpers

    private static async Task<JsonNode> Api(string host, string path, string method = "GET",
        object? body = null, string? token = null)
    {
        using var client = new HttpClient { BaseAddress = new Uri(host) };
        if (token != null) client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var request = new HttpRequestMessage(new HttpMethod(method), path);
        if (body != null) request.Content = JsonContent.Create(body);
        var response = await client.SendAsync(request);
        var text = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, $"{method} {host}{path} -> {(int)response.StatusCode}: {text}");
        return string.IsNullOrEmpty(text) ? new JsonObject() : JsonNode.Parse(text)!;
    }

    private async Task<string> Token(string username, string password) =>
        (await Api(AuthUrl, "/api/auth/login", "POST", new { username, password }))["token"]!.GetValue<string>();

    private object NewUser(string username) => new
    {
        username,
        email = username + "@example.com",
        password = pw,
        confirmPassword = pw,
        firstName = "QA",
        lastName = "Tracking",
        contactNumber = "0771234567"
    };

    /// <summary>Creates driver + dispatcher + vehicle + trip and drives it to In-Progress via the real APIs.</summary>
    private async Task<Scenario> StartInProgressTripAsync()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var admin = await Token(AdminUsername, AdminPassword);

        var driverUser = "qatd" + suffix;
        var driver = await Api(AuthUrl, "/api/users/drivers", "POST", NewUser(driverUser), admin);
        var driverId = driver["id"]!.GetValue<string>();
        var driverToken = await Token(driverUser, pw);

        var dispatcherUser = "qatp" + suffix;
        await Api(AuthUrl, "/api/auth/register/dispatcher", "POST", NewUser(dispatcherUser));
        var dispatcherToken = await Token(dispatcherUser, pw);

        var capacity = Random.Shared.Next(600000, 900000);
        var reg = "QT" + suffix.ToUpperInvariant();
        var vehicle = await Api(FleetUrl, "/api/vehicles", "POST",
            new { registrationNo = reg, type = "Van", capacity = capacity + " persons" }, admin);

        var pickup = "QA Track " + suffix;
        var trip = await Api(TripUrl, "/api/trips", "POST", new
        {
            pickupLocation = pickup,
            dropoffLocation = "QA Track Destination",
            requiredCapacity = capacity + " persons"
        }, dispatcherToken);
        var tripId = trip["id"]!.GetValue<string>();

        await Api(TripUrl, $"/api/trips/{tripId}/assign", "POST",
            new { vehicleId = vehicle["id"]!.GetValue<string>(), driverId }, dispatcherToken);
        await Api(TripUrl, $"/api/trips/{tripId}/respond", "POST", new { decision = "accept" }, driverToken);

        return new Scenario(tripId, pickup, reg, driverUser, driverToken, dispatcherUser);
    }

    private static Task PostPingAsync(Scenario s, DateTime observedAtUtc) =>
        Api(TrackingUrl, "/api/tracking/locations", "POST", new
        {
            tripId = s.TripId,
            latitude = 6.9271m,
            longitude = 79.8612m,
            observedAt = observedAtUtc
        }, s.DriverToken);

    // ---------------------------------------------------------------- UI helpers

    private void Go(string path) => Driver.Navigate().GoToUrl(FrontendBaseUrl + path);

    private void LoginFresh(string username)
    {
        Go("/login");
        ((IJavaScriptExecutor)Driver).ExecuteScript("localStorage.clear()");
        LoginAs(username, pw);
    }

    private IWebElement ListItem(string pickup) =>
        Wait.Until(d => d.FindElements(By.CssSelector(".vehicle-list-item"))
            .FirstOrDefault(e => e.Text.Contains(pickup)))!;

    private void WaitForItemText(string pickup, string expected) =>
        Wait.Until(d => d.FindElements(By.CssSelector(".vehicle-list-item"))
            .Any(e => e.Text.Contains(pickup) && e.Text.Contains(expected, StringComparison.OrdinalIgnoreCase)));

    private void OpenTrackingAs(string dispatcherUser)
    {
        LoginFresh(dispatcherUser);
        Go("/dispatcher/tracking");
        Wait.Until(d => d.FindElements(By.TagName("h1")).Any(h => h.Text.Contains("Fleet Tracking")));
    }

    private void ClickRefreshNow() =>
        Wait.Until(d => d.FindElements(By.XPath("//button[contains(normalize-space(.),'Refresh now')]"))
            .FirstOrDefault(b => b.Displayed && b.Enabled))!.Click();

    private void Shot(string name)
    {
        Directory.CreateDirectory(ShotDir);
        ((ITakesScreenshot)Driver).GetScreenshot().SaveAsFile(Path.Combine(ShotDir, name + ".png"));
    }

    private async Task Run(string name, Func<Task> body)
    {
        try { await body(); Shot(name + "-Pass"); }
        catch { Shot(name + "-Fail"); throw; }
    }

    // ---------------------------------------------------------------- tests

    [Sprint3BrowserFact]
    [Trait("Category", "Sprint4Live")]
    [Trait("Story", "US-15")]
    public Task TC15_01_InProgressTripWithNoPing_ShowsUnavailableInVehicleList() => Run("TC15-01", async () =>
    {
        var s = await StartInProgressTripAsync();
        OpenTrackingAs(s.DispatcherUser);

        var item = ListItem(s.Pickup);
        Assert.Contains("No location reported yet", item.Text);
        Assert.Contains("unavailable", item.Text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("QA Track Destination", item.Text);
    });

    [Sprint3BrowserFact]
    [Trait("Category", "Sprint4Live")]
    [Trait("Story", "US-15")]
    public Task TC15_02_RecentPing_ShowsFreshBadgeMarkerAndVehicleLabel() => Run("TC15-02", async () =>
    {
        var s = await StartInProgressTripAsync();
        await PostPingAsync(s, DateTime.UtcNow.AddSeconds(-5));
        OpenTrackingAs(s.DispatcherUser);

        WaitForItemText(s.Pickup, "fresh");
        var item = ListItem(s.Pickup);
        Assert.DoesNotContain("No location reported yet", item.Text);
        Assert.Contains("Updated", item.Text);

        // Leaflet draws CircleMarker as an SVG path; the permanent tooltip carries the registration number.
        Wait.Until(d => d.FindElements(By.CssSelector("path.leaflet-interactive")).Count >= 1);
        Wait.Until(d => d.FindElements(By.CssSelector(".leaflet-tooltip"))
            .Any(t => t.Text.Contains(s.VehicleReg)));
    });

    [Sprint3BrowserFact]
    [Trait("Category", "Sprint4Live")]
    [Trait("Story", "US-15")]
    public Task TC15_03_PingOlderThanFiveMinutes_ShowsStaleBadge() => Run("TC15-03", async () =>
    {
        var s = await StartInProgressTripAsync();
        await PostPingAsync(s, DateTime.UtcNow.AddMinutes(-10));
        OpenTrackingAs(s.DispatcherUser);

        WaitForItemText(s.Pickup, "stale");
        Assert.DoesNotContain("fresh", ListItem(s.Pickup).Text, StringComparison.OrdinalIgnoreCase);
    });

    [Sprint3BrowserFact]
    [Trait("Category", "Sprint4Live")]
    [Trait("Story", "US-15")]
    public Task TC15_04_RefreshNow_PicksUpANewPingWithoutReload() => Run("TC15-04", async () =>
    {
        var s = await StartInProgressTripAsync();
        OpenTrackingAs(s.DispatcherUser);
        Assert.Contains("No location reported yet", ListItem(s.Pickup).Text);

        await PostPingAsync(s, DateTime.UtcNow.AddSeconds(-2));
        ClickRefreshNow();

        WaitForItemText(s.Pickup, "fresh");
        Assert.DoesNotContain("No location reported yet", ListItem(s.Pickup).Text);
        Assert.Contains(Driver.FindElements(By.CssSelector(".tracking-refreshed-at")), e => e.Text.StartsWith("Updated"));
    });

    [Sprint3BrowserFact]
    [Trait("Category", "Sprint4Live")]
    [Trait("Story", "US-15")]
    public Task TC15_05_Driver_IsBlockedFromFleetTrackingPage() => Run("TC15-05", async () =>
    {
        var s = await StartInProgressTripAsync();
        LoginFresh(s.DriverUser);
        Go("/dispatcher/tracking");

        Wait.Until(d => d.Url.Contains("/403"));
        Assert.DoesNotContain("Fleet Tracking", Driver.FindElement(By.TagName("body")).Text);
    });

    [Sprint3BrowserFact]
    [Trait("Category", "Sprint4Live")]
    [Trait("Story", "US-15")]
    public Task TC15_06_NotLoggedIn_IsRedirectedToLogin() => Run("TC15-06", async () =>
    {
        Go("/login");
        ((IJavaScriptExecutor)Driver).ExecuteScript("localStorage.clear()");
        Go("/dispatcher/tracking");

        Wait.Until(d => d.Url.Contains("/login"));
        await Task.CompletedTask;
    });
}
