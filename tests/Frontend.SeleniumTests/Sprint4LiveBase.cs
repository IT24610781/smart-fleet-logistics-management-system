using System.Globalization;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using OpenQA.Selenium;
using Xunit;

namespace Frontend.SeleniumTests;

/// <summary>
/// Shared plumbing for the Sprint 4 live Selenium tests.
/// Same opt-in rules as TrackingPageTests:
///   set QA_RUN_SPRINT3_UI=1
///   set "QA_ADMIN_PASSWORD=..."          (QA_ADMIN_USERNAME defaults to admin)
/// URLs default to 127.0.0.1 (avoids the Docker Desktop "localhost" dropped-connection problem):
///   QA_AUTH_URL 5100, QA_FLEET_URL 5041, QA_TRIP_URL 5051, QA_TRACKING_URL 5000, QA_FRONTEND_URL http://localhost:5173
/// Every test builds its own driver, vehicle and trip through the real APIs, so tests do not depend on seeded data.
/// Concrete test classes must carry [Collection("Sprint4 live")] (defined in TrackingPageTests.cs) so they run one at a time.
/// </summary>
public abstract class Sprint4LiveBase : Sprint3SeleniumTestBase
{
    protected static readonly string AuthUrl = Env("QA_AUTH_URL", "http://127.0.0.1:5100");
    protected static readonly string FleetUrl = Env("QA_FLEET_URL", "http://127.0.0.1:5041");
    protected static readonly string TripUrl = Env("QA_TRIP_URL", "http://127.0.0.1:5051");
    protected static readonly string TrackingUrl = Env("QA_TRACKING_URL", "http://127.0.0.1:5000");
    protected static readonly string AdminUsername = Env("QA_ADMIN_USERNAME", "admin");
    protected static string AdminPassword => Environment.GetEnvironmentVariable("QA_ADMIN_PASSWORD")
        ?? throw new InvalidOperationException("Set QA_ADMIN_PASSWORD before running live QA.");

    private static readonly string ShotDir = Path.Combine(
        Environment.GetEnvironmentVariable("QA_OUTPUT_ROOT") ?? Path.Combine("TestResults", "QA-Sprint04"),
        "ui-screenshots");

    protected readonly string Pw = "QaS4!" + Guid.NewGuid().ToString("N");

    private static string Env(string name, string fallback) => Environment.GetEnvironmentVariable(name) ?? fallback;

    protected sealed record DriverAccount(string Id, string Username, string LastName, string Token);

    protected sealed record Scenario(
        string TripId, string Pickup, string VehicleReg, DriverAccount Driver, string DispatcherUser, string DispatcherToken);

    // ------------------------------------------------------------------ API helpers

    /// <summary>Throws on an HTTP error. Connection-level failures are retried (Docker Desktop on Windows drops some).</summary>
    protected static async Task<JsonNode> Api(string host, string path, string method = "GET",
        object? body = null, string? token = null)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                using var client = new HttpClient { BaseAddress = new Uri(host), Timeout = TimeSpan.FromSeconds(30) };
                if (token != null) client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
                using var request = new HttpRequestMessage(new HttpMethod(method), path);
                if (body != null) request.Content = JsonContent.Create(body);
                using var response = await client.SendAsync(request);
                var text = await response.Content.ReadAsStringAsync();
                Assert.True(response.IsSuccessStatusCode, $"{method} {host}{path} -> {(int)response.StatusCode}: {text}");
                return string.IsNullOrWhiteSpace(text) ? new JsonObject() : JsonNode.Parse(text)!;
            }
            catch (HttpRequestException) when (attempt < 3)
            {
                await Task.Delay(1000 * attempt);
            }
        }
    }

    protected async Task<string> TokenAsync(string username, string password) =>
        (await Api(AuthUrl, "/api/auth/login", "POST", new { username, password }))["token"]!.GetValue<string>();

    protected Task<string> AdminTokenAsync() => TokenAsync(AdminUsername, AdminPassword);

    private object NewUser(string username, string lastName) => new
    {
        username,
        email = username + "@example.com",
        password = Pw,
        confirmPassword = Pw,
        firstName = "QA",
        lastName,
        contactNumber = "0771234567"
    };

    /// <summary>Creates a driver with a unique last name, so its row in the driver report can be found.</summary>
    protected async Task<DriverAccount> CreateDriverAsync(string adminToken)
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var username = "qasd" + suffix;
        var lastName = "Zq" + suffix;
        var created = await Api(AuthUrl, "/api/users/drivers", "POST", NewUser(username, lastName), adminToken);
        return new DriverAccount(created["id"]!.GetValue<string>(), username, lastName, await TokenAsync(username, Pw));
    }

    /// <summary>Creates driver, dispatcher, vehicle and trip and assigns the trip.
    /// accept = true: the driver accepts, so the trip is In-Progress. accept = false: it stays Assigned.</summary>
    protected async Task<Scenario> CreateTripAsync(bool accept)
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var admin = await AdminTokenAsync();
        var driver = await CreateDriverAsync(admin);

        var dispatcherUser = "qasp" + suffix;
        await Api(AuthUrl, "/api/auth/register/dispatcher", "POST", NewUser(dispatcherUser, "Zp" + suffix));
        var dispatcherToken = await TokenAsync(dispatcherUser, Pw);

        var capacity = Random.Shared.Next(600000, 900000) + " persons";
        var reg = "QS" + suffix.ToUpperInvariant();
        var vehicle = await Api(FleetUrl, "/api/vehicles", "POST",
            new { registrationNo = reg, type = "Van", capacity }, admin);

        var pickup = "QA S4 " + suffix;
        var trip = await Api(TripUrl, "/api/trips", "POST", new
        {
            pickupLocation = pickup,
            dropoffLocation = "QA S4 Destination",
            requiredCapacity = capacity
        }, dispatcherToken);
        var tripId = trip["id"]!.GetValue<string>();

        await Api(TripUrl, $"/api/trips/{tripId}/assign", "POST",
            new { vehicleId = vehicle["id"]!.GetValue<string>(), driverId = driver.Id }, dispatcherToken);

        if (accept)
        {
            await Api(TripUrl, $"/api/trips/{tripId}/respond", "POST", new { decision = "accept" }, driver.Token);
        }

        return new Scenario(tripId, pickup, reg, driver, dispatcherUser, dispatcherToken);
    }

    protected static Task AcceptTripAsync(Scenario s) =>
        Api(TripUrl, $"/api/trips/{s.TripId}/respond", "POST", new { decision = "accept" }, s.Driver.Token);

    protected static Task RejectTripAsync(Scenario s) =>
        Api(TripUrl, $"/api/trips/{s.TripId}/respond", "POST", new { decision = "reject" }, s.Driver.Token);

    protected static Task CompleteTripAsync(Scenario s) =>
        Api(TripUrl, $"/api/trips/{s.TripId}/complete", "POST", null, s.Driver.Token);

    /// <summary>Polls the history endpoint until it has at least atLeast pings or the time runs out.</summary>
    protected static async Task<JsonArray> WaitForPingsAsync(string tripId, string token, int atLeast, int seconds = 45)
    {
        var deadline = DateTime.UtcNow.AddSeconds(seconds);
        while (true)
        {
            var rows = (await Api(TrackingUrl, $"/api/tracking/trips/{tripId}/locations", "GET", null, token)).AsArray();
            if (rows.Count >= atLeast || DateTime.UtcNow > deadline) return rows;
            await Task.Delay(1500);
        }
    }

    // ---------------------------------------------------------------------- UI helpers

    protected void Go(string path) => Driver.Navigate().GoToUrl(FrontendBaseUrl + path);

    protected void LoginFresh(string username, string password)
    {
        Go("/login");
        ((IJavaScriptExecutor)Driver).ExecuteScript("localStorage.clear()");
        LoginAs(username, password);
    }

    protected void OpenReportsAsAdmin()
    {
        LoginFresh(AdminUsername, AdminPassword);
        Go("/admin/reports");
        Wait.Until(d => d.FindElements(By.TagName("h1")).Any(h => h.Text.Contains("Reports")));
    }

    /// <summary>The report card whose heading is exactly <paramref name="title"/>.</summary>
    protected IWebElement Card(string title) =>
        Wait.Until(d => d.FindElements(
            By.XPath($"//h3[normalize-space()='{title}']/ancestor::div[contains(@class,'card')][1]")).FirstOrDefault())!;

    /// <summary>Waits until the card has stopped showing its loading text.</summary>
    protected void WaitUntilLoaded(IWebElement card) =>
        Wait.Until(_ => !card.Text.Contains("Calculating") && !card.Text.Contains("Loading trips"));

    protected static int? TryMetric(IWebElement card, string label)
    {
        var cells = card.FindElements(By.XPath(
            $".//div[contains(@class,'report-metric-row')][span[1][normalize-space()='{label}']]/span[2]"));
        return cells.Count > 0 && int.TryParse(cells[0].Text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var n)
            ? n : null;
    }

    protected int Metric(IWebElement card, string label) =>
        Wait.Until(_ => TryMetric(card, label))!.Value;

    protected void WaitForMetric(IWebElement card, string label, int expected) =>
        Wait.Until(_ => TryMetric(card, label) == expected);

    protected static void ClickRefresh(IWebElement card) =>
        card.FindElement(By.XPath(".//button[normalize-space()='Refresh']")).Click();

    protected void Shot(string name)
    {
        Directory.CreateDirectory(ShotDir);
        ((ITakesScreenshot)Driver).GetScreenshot().SaveAsFile(Path.Combine(ShotDir, name + ".png"));
    }

    protected async Task Run(string name, Func<Task> body)
    {
        try { await body(); Shot(name + "-Pass"); }
        catch { Shot(name + "-Fail"); throw; }
    }
}
