using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Support.UI;

namespace Frontend.SeleniumTests;

/// <summary>Live Sprint 3 QA is opt-in because it creates accounts, vehicles and trips.</summary>
public sealed class Sprint3BrowserFactAttribute : FactAttribute
{
    public Sprint3BrowserFactAttribute()
    {
        if (Environment.GetEnvironmentVariable("QA_RUN_SPRINT3_UI") != "1")
            Skip = "Set QA_RUN_SPRINT3_UI=1 and QA_ADMIN_PASSWORD; see tests/qa-sprint3/README.md.";
    }
}

public abstract class Sprint3SeleniumTestBase : IDisposable
{
    protected static readonly string FrontendBaseUrl =
        Environment.GetEnvironmentVariable("QA_FRONTEND_URL") ?? "http://localhost:5173";
    protected IWebDriver Driver { get; }
    protected WebDriverWait Wait { get; }

    protected Sprint3SeleniumTestBase()
    {
        var options = new ChromeOptions();
        options.AddArgument("--window-size=1280,900");
        options.AddArgument("--disable-gpu");
        if (Environment.GetEnvironmentVariable("QA_SELENIUM_HEADLESS") != "0")
            options.AddArgument("--headless=new");
        Driver = new ChromeDriver(ChromeDriverService.CreateDefaultService(), options,
            TimeSpan.FromSeconds(120));
        Wait = new WebDriverWait(Driver, TimeSpan.FromSeconds(30));
        // React replaces rows after a response; reacquire them on the next poll.
        Wait.IgnoreExceptionTypes(typeof(StaleElementReferenceException));
    }

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
        try { Driver.Quit(); }
        finally { Driver.Dispose(); }
    }
}

