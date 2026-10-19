using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using OpenQA.Selenium;
using Xunit;

namespace Frontend.SeleniumTests;

/// <summary>
/// SCRUM-154 / US-17 to US-19: the Reports page (/admin/reports) is Admin-only.
/// Role guard tests only, so they do not depend on report content (RA is still finishing US-17 to US-19).
///
/// Same opt-in switches as TrackingPageTests:
///   QA_RUN_SPRINT3_UI=1, QA_ADMIN_PASSWORD (required), QA_ADMIN_USERNAME (default "admin"),
///   QA_FRONTEND_URL, QA_AUTH_URL, QA_TRACKING_URL.
///
/// RP_06 is a PROBE: it is expected to FAIL while the backend still lets Dispatchers call the report
/// endpoints. A failure there is a defect candidate, not a broken test.
/// </summary>
[Collection("Sprint4 live")]
public class ReportsPageTests : Sprint3SeleniumTestBase
{
    private static readonly string AuthUrl = Environment.GetEnvironmentVariable("QA_AUTH_URL") ?? "http://127.0.0.1:5100";
    private static readonly string TrackingUrl = Environment.GetEnvironmentVariable("QA_TRACKING_URL") ?? "http://127.0.0.1:5000";
    private static readonly string AdminUsername = Environment.GetEnvironmentVariable("QA_ADMIN_USERNAME") ?? "admin";
    private static string AdminPassword => Environment.GetEnvironmentVariable("QA_ADMIN_PASSWORD")
        ?? throw new InvalidOperationException("Set QA_ADMIN_PASSWORD before running live QA.");

    private static readonly string ShotDir = Path.Combine(
        Environment.GetEnvironmentVariable("QA_OUTPUT_ROOT") ?? Path.Combine("TestResults", "QA-Sprint04"),
        "ui-screenshots");

    private readonly string pw = "QaRep!" + Guid.NewGuid().ToString("N");

    // ---------------------------------------------------------------- API helpers

    private static async Task<(int Status, string Body)> Call(string host, string path, string method = "GET",
        object? body = null, string? token = null)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                using var client = new HttpClient { BaseAddress = new Uri(host), Timeout = TimeSpan.FromSeconds(60) };
                if (token != null) client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
                var request = new HttpRequestMessage(new HttpMethod(method), path);
                if (body != null) request.Content = JsonContent.Create(body);
                var response = await client.SendAsync(request);
                return ((int)response.StatusCode, await response.Content.ReadAsStringAsync());
            }
            catch (HttpRequestException) when (attempt < 3)
            {
                await Task.Delay(1500); // Docker Desktop on Windows sometimes drops the first connection
            }
        }
    }

    private static async Task<JsonNode> Api(string host, string path, string method = "GET",
        object? body = null, string? token = null)
    {
        var (status, text) = await Call(host, path, method, body, token);
        Assert.True(status is >= 200 and < 300, $"{method} {host}{path} -> {status}: {text}");
        return string.IsNullOrEmpty(text) ? new JsonObject() : JsonNode.Parse(text)!;
    }

    private static async Task<string> Token(string username, string password) =>
        (await Api(AuthUrl, "/api/auth/login", "POST", new { username, password }))["token"]!.GetValue<string>();

    private object NewUser(string username) => new
    {
        username,
        email = username + "@example.com",
        password = pw,
        confirmPassword = pw,
        firstName = "QA",
        lastName = "Reports",
        contactNumber = "0771234567"
    };

    private async Task<string> CreateDispatcherAsync()
    {
        var name = "qard" + Guid.NewGuid().ToString("N")[..8];
        await Api(AuthUrl, "/api/auth/register/dispatcher", "POST", NewUser(name));
        return name;
    }

    private async Task<string> CreateDriverAsync()
    {
        var name = "qarv" + Guid.NewGuid().ToString("N")[..8];
        var admin = await Token(AdminUsername, AdminPassword);
        await Api(AuthUrl, "/api/users/drivers", "POST", NewUser(name), admin);
        return name;
    }

    // ---------------------------------------------------------------- UI helpers

    private void Go(string path) => Driver.Navigate().GoToUrl(FrontendBaseUrl + path);

    private void LoginFresh(string username, string password)
    {
        Go("/login");
        ((IJavaScriptExecutor)Driver).ExecuteScript("localStorage.clear()");
        LoginAs(username, password);
    }

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
    [Trait("Story", "US-17")]
    public Task RP_01_Admin_CanOpenReportsPage() => Run("RP-01", async () =>
    {
        LoginFresh(AdminUsername, AdminPassword);
        Go("/admin/reports");

        Wait.Until(d => d.FindElements(By.TagName("h1")).Any(h => h.Text.Trim() == "Reports"));
        Assert.Contains("/admin/reports", Driver.Url);
        Assert.DoesNotContain("Access denied", Driver.FindElement(By.TagName("body")).Text);
        await Task.CompletedTask;
    });

    [Sprint3BrowserFact]
    [Trait("Category", "Sprint4Live")]
    [Trait("Story", "US-17")]
    public Task RP_02_Dispatcher_IsBlockedFromReportsPage() => Run("RP-02", async () =>
    {
        var dispatcher = await CreateDispatcherAsync();
        LoginFresh(dispatcher, pw);
        Go("/admin/reports");

        Wait.Until(d => d.Url.Contains("/403"));
        Assert.Contains("Access denied", Driver.FindElement(By.TagName("body")).Text);
    });

    [Sprint3BrowserFact]
    [Trait("Category", "Sprint4Live")]
    [Trait("Story", "US-17")]
    public Task RP_03_Driver_IsBlockedFromReportsPage() => Run("RP-03", async () =>
    {
        var driver = await CreateDriverAsync();
        LoginFresh(driver, pw);
        Go("/admin/reports");

        Wait.Until(d => d.Url.Contains("/403"));
        Assert.Contains("Access denied", Driver.FindElement(By.TagName("body")).Text);
    });

    [Sprint3BrowserFact]
    [Trait("Category", "Sprint4Live")]
    [Trait("Story", "US-17")]
    public Task RP_04_NotLoggedIn_IsRedirectedToLogin() => Run("RP-04", async () =>
    {
        Go("/login");
        ((IJavaScriptExecutor)Driver).ExecuteScript("localStorage.clear()");
        Go("/admin/reports");

        Wait.Until(d => d.Url.Contains("/login"));
        await Task.CompletedTask;
    });

    [Sprint3BrowserFact]
    [Trait("Category", "Sprint4Live")]
    [Trait("Story", "US-17")]
    public Task RP_05_ReportsLink_IsShownToAdminButNotToDispatcher() => Run("RP-05", async () =>
    {
        LoginFresh(AdminUsername, AdminPassword);
        Wait.Until(d => d.FindElements(By.CssSelector("a[href='/admin/reports']")).Count == 1);

        var dispatcher = await CreateDispatcherAsync();
        LoginFresh(dispatcher, pw);
        Wait.Until(d => d.FindElements(By.CssSelector("a[href^='/dispatcher/']")).Count > 0); // sidebar rendered
        Assert.Empty(Driver.FindElements(By.CssSelector("a[href='/admin/reports']")));
    });

    // PROBE: expected to FAIL today. The sprint says reports are for Admin only, but the backend still lets
    // Dispatchers call the report endpoints. If this fails with 200, log it as a defect (authorisation too wide).
    [Sprint3BrowserFact]
    [Trait("Category", "Sprint4Live")]
    [Trait("Story", "US-17")]
    [Trait("Kind", "Probe")]
    public async Task RP_06_Probe_Dispatcher_CannotCallFleetUtilizationApi()
    {
        var dispatcher = await CreateDispatcherAsync();
        var token = await Token(dispatcher, pw);
        var start = Uri.EscapeDataString(DateTime.UtcNow.AddDays(-7).ToString("o"));
        var end = Uri.EscapeDataString(DateTime.UtcNow.ToString("o"));

        var (status, body) = await Call(TrackingUrl, $"/api/reports/fleet-utilization?start={start}&end={end}", "GET", null, token);

        Assert.True(status == 403, $"Dispatcher got HTTP {status} from fleet-utilization; expected 403 (reports are Admin-only). Body: {body}");
    }
}
