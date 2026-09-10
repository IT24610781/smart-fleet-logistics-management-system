using OpenQA.Selenium;

namespace Frontend.SeleniumTests;

/// <summary>
/// US-03: User Logout
/// End-to-end browser tests for the real "Logout" button — this is
/// the actual testable surface for this story, since logout has no
/// backend endpoint at all (see LogoutArchitectureTests.cs on the
/// backend side for that finding).
///
/// Shares "Selenium collection" with the other browser-test classes so
/// xUnit runs them serially — only one real Chrome/ChromeDriver session
/// exists at a time. Without this, xUnit parallelizes across classes by
/// default, which launches several real browsers simultaneously and
/// starves them of CPU, causing WebDriverException timeouts that look
/// like app/network failures but are actually resource contention.
/// </summary>
[Collection("Selenium collection")]
public class LogoutPageTests : SeleniumTestBase
{
    private async Task<(string Username, string Password)> LoginFreshAsync(string suffix)
    {
        var (username, password) = await TestDataSeeder.CreateFreshDriverAsync(suffix);

        Driver.Navigate().GoToUrl($"{FrontendBaseUrl}/login");
        Wait.Until(d => d.FindElement(By.Id("username")));
        Driver.FindElement(By.Id("username")).SendKeys(username);
        Driver.FindElement(By.Id("password")).SendKeys(password);
        Driver.FindElement(By.CssSelector("button[type='submit']")).Click();
        Wait.Until(d => !d.Url.Contains("/login"));

        return (username, password);
    }

    // ============================================================
    // Scenario 1 - Successful logout
    // ============================================================

    [Fact]
    public async Task Logout_ClearsTokenAndRedirectsToLogin()
    {
        await LoginFreshAsync("logout1");

        Driver.FindElement(By.CssSelector(".topbar-logout")).Click();

        Wait.Until(d => d.Url.Contains("/login"));
        Assert.Contains("/login", Driver.Url);

        // DoD: "Clicking Logout clears the token from the client."
        var js = (IJavaScriptExecutor)Driver;
        var token = js.ExecuteScript("return localStorage.getItem('smartfleet_token');");
        var user = js.ExecuteScript("return localStorage.getItem('smartfleet_user');");

        Assert.Null(token);
        Assert.Null(user);
    }

    [Fact]
    public async Task Logout_ThenAccessingProtectedRoute_RedirectsToLogin()
    {
        await LoginFreshAsync("logout2");

        Driver.FindElement(By.CssSelector(".topbar-logout")).Click();
        Wait.Until(d => d.Url.Contains("/login"));

        // AC: "I can no longer access protected routes until I log in
        // again." Try navigating directly to the dashboard afterward.
        Driver.Navigate().GoToUrl($"{FrontendBaseUrl}/driver/dashboard");

        Wait.Until(d => d.Url.Contains("/login"));
        Assert.Contains("/login", Driver.Url);
    }
}
