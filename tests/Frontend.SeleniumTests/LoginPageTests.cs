using OpenQA.Selenium;

namespace Frontend.SeleniumTests;

/// <summary>
/// US-02: User Login with JWT
/// End-to-end browser tests driving the real /login page against the
/// real backend, plus real protected-route redirect behavior.
///
/// Shares "Selenium collection" with the other browser-test classes so
/// xUnit runs them serially — only one real Chrome/ChromeDriver session
/// exists at a time. Without this, xUnit parallelizes across classes by
/// default, which launches several real browsers simultaneously and
/// starves them of CPU, causing WebDriverException timeouts that look
/// like app/network failures but are actually resource contention.
/// </summary>
[Collection("Selenium collection")]
public class LoginPageTests : SeleniumTestBase
{
    // ============================================================
    // Scenario 1 - Successful login
    // ============================================================

    [Fact]
    public async Task Login_ValidCredentials_RedirectsToRoleDashboard()
    {
        var (username, password) = await TestDataSeeder.CreateFreshDriverAsync("login1");

        Driver.Navigate().GoToUrl($"{FrontendBaseUrl}/login");
        Wait.Until(d => d.FindElement(By.Id("username")));

        Driver.FindElement(By.Id("username")).SendKeys(username);
        Driver.FindElement(By.Id("password")).SendKeys(password);
        Driver.FindElement(By.CssSelector("button[type='submit']")).Click();

        // Newly created accounts are always role "Driver" (see US-01's
        // documented role-hardcoding gap), so the expected redirect
        // target is the driver dashboard.
        Wait.Until(d => d.Url.Contains("/driver/dashboard"));
        Assert.Contains("/driver/dashboard", Driver.Url);
    }

    // ============================================================
    // Scenario 2 - Invalid credentials
    // ============================================================

    [Fact]
    public async Task Login_WrongPassword_ShowsGenericErrorAndStaysOnLoginPage()
    {
        var (username, _) = await TestDataSeeder.CreateFreshDriverAsync("login2");

        Driver.Navigate().GoToUrl($"{FrontendBaseUrl}/login");
        Wait.Until(d => d.FindElement(By.Id("username")));

        Driver.FindElement(By.Id("username")).SendKeys(username);
        Driver.FindElement(By.Id("password")).SendKeys("TotallyWrongPassword");
        Driver.FindElement(By.CssSelector("button[type='submit']")).Click();

        var errorBanner = Wait.Until(d => d.FindElement(By.CssSelector("[role='alert']")));
        Assert.Contains("Invalid username or password.", errorBanner.Text);

        Assert.Contains("/login", Driver.Url);
    }

    [Fact]
    public void Login_NonExistentUsername_ShowsSameGenericError()
    {
        Driver.Navigate().GoToUrl($"{FrontendBaseUrl}/login");
        Wait.Until(d => d.FindElement(By.Id("username")));

        Driver.FindElement(By.Id("username")).SendKeys($"does_not_exist_{Guid.NewGuid():N}");
        Driver.FindElement(By.Id("password")).SendKeys("whatever123");
        Driver.FindElement(By.CssSelector("button[type='submit']")).Click();

        var errorBanner = Wait.Until(d => d.FindElement(By.CssSelector("[role='alert']")));
        Assert.Contains("Invalid username or password.", errorBanner.Text);
    }

    // ============================================================
    // Scenario 3 - Protected route access
    // ============================================================

    [Fact]
    public void ProtectedRoute_NotLoggedIn_RedirectsToLogin()
    {
        // No login at all — go straight to a protected page.
        Driver.Navigate().GoToUrl($"{FrontendBaseUrl}/driver/dashboard");

        Wait.Until(d => d.Url.Contains("/login"));
        Assert.Contains("/login", Driver.Url);
    }

    [Fact]
    public async Task ProtectedRoute_LoggedIn_AllowsAccess()
    {
        var (username, password) = await TestDataSeeder.CreateFreshDriverAsync("login3");

        Driver.Navigate().GoToUrl($"{FrontendBaseUrl}/login");
        Wait.Until(d => d.FindElement(By.Id("username")));
        Driver.FindElement(By.Id("username")).SendKeys(username);
        Driver.FindElement(By.Id("password")).SendKeys(password);
        Driver.FindElement(By.CssSelector("button[type='submit']")).Click();

        Wait.Until(d => d.Url.Contains("/driver/dashboard"));

        // Directly hitting the protected URL again (e.g. a refresh or
        // bookmark) should stay on the page, not bounce to /login.
        Driver.Navigate().GoToUrl($"{FrontendBaseUrl}/driver/dashboard");
        Wait.Until(d => !d.Url.Contains("/login"));
        Assert.Contains("/driver/dashboard", Driver.Url);
    }
}
