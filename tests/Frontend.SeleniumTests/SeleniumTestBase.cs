using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Support.UI;

namespace Frontend.SeleniumTests;

/// <summary>
/// Base fixture for browser tests against the REAL running app.
///
/// PREREQUISITES — both must already be running before these tests
/// are executed (Selenium does not start them for you):
///   1. AuthUserService: `dotnet run` from services/AuthUserService.
///      Must be reachable at http://localhost:5000 (Kestrel's default
///      when no launchSettings.json exists — matches the frontend's
///      own default baseURL in src/services/api.js).
///   2. Frontend: `npm run dev` from /frontend. Must be reachable at
///      http://localhost:5173 (Vite's default port).
///
/// These tests drive an actual Chrome browser via ChromeDriver, so
/// Chrome must be installed locally. No headless flag is set by
/// default so you can watch it run; add "--headless=new" to run it
/// invisibly (e.g. in CI).
/// </summary>
public abstract class SeleniumTestBase : IDisposable
{
    protected const string FrontendBaseUrl = "http://localhost:5173";

    protected IWebDriver Driver { get; }
    protected WebDriverWait Wait { get; }

    protected SeleniumTestBase()
    {
        var options = new ChromeOptions();
        options.AddArgument("--window-size=1280,900");
        // Reduces per-instance CPU/memory load, which matters a lot when
        // many Chrome instances are created across a full test run.
        options.AddArgument("--disable-gpu");
        options.AddArgument("--disable-extensions");
        options.AddArgument("--disable-dev-shm-usage");
        options.AddArgument("--no-sandbox");
        // Uncomment to run without a visible browser window:
        // options.AddArgument("--headless=new");

        // The 60-second default here is the Selenium .NET client's
        // command timeout talking to the chromedriver process itself —
        // separate from our own WebDriverWait below. On a machine under
        // load from many prior Chrome/ChromeDriver instances in one
        // run, that internal command channel itself can occasionally
        // take longer than 60s to respond, causing a WebDriverException
        // even though the test logic is fine. Raising it to 120s gives
        // real headroom without masking a genuine app problem.
        Driver = new ChromeDriver(ChromeDriverService.CreateDefaultService(), options, TimeSpan.FromSeconds(120));
        Wait = new WebDriverWait(Driver, TimeSpan.FromSeconds(30));
    }

    /// <summary>
    /// Logs in through the real login page UI (not an API shortcut) and
    /// waits until redirected away from /login, proving the full login
    /// flow works end-to-end before a test proceeds.
    /// </summary>
    protected void LoginAs(string username, string password)
    {
        Driver.Navigate().GoToUrl($"{FrontendBaseUrl}/login");

        Wait.Until(d => d.FindElement(By.Id("username")));
        Driver.FindElement(By.Id("username")).SendKeys(username);
        Driver.FindElement(By.Id("password")).SendKeys(password);
        Driver.FindElement(By.CssSelector("button[type='submit']")).Click();

        Wait.Until(d => !d.Url.Contains("/login"));
    }

    public void Dispose()
    {
        Driver.Quit();
        Driver.Dispose();
    }
}
