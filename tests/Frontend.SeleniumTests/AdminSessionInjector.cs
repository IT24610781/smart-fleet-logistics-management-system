using OpenQA.Selenium;
using OpenQA.Selenium.Support.UI;

namespace Frontend.SeleniumTests;

/// <summary>
/// Signs the browser in as the REAL seeded Admin through the login form.
///
/// The earlier version minted a fake Admin token for a user that does not exist in the
/// database. The system now seeds a real admin account (AdminSeeder), so tests log in
/// the same way a person would. Set QA_ADMIN_PASSWORD first; QA_ADMIN_USERNAME defaults
/// to "admin".
/// </summary>
public static class AdminSessionInjector
{
    public static void LoginAsAdmin(IWebDriver driver, string frontendBaseUrl)
    {
        var username = Environment.GetEnvironmentVariable("QA_ADMIN_USERNAME") ?? "admin";
        var password = Environment.GetEnvironmentVariable("QA_ADMIN_PASSWORD")
            ?? throw new InvalidOperationException("Set QA_ADMIN_PASSWORD before running Admin UI tests.");

        var wait = new WebDriverWait(driver, TimeSpan.FromSeconds(30));
        driver.Navigate().GoToUrl($"{frontendBaseUrl}/login");
        wait.Until(d => d.FindElement(By.Id("username")));
        driver.FindElement(By.Id("username")).SendKeys(username);
        driver.FindElement(By.Id("password")).SendKeys(password);
        driver.FindElement(By.CssSelector("button[type='submit']")).Click();
        wait.Until(d => !d.Url.Contains("/login"));
    }

    // Kept so the existing tests compile without edits.
    public static void LoginAsAdminViaLocalStorage(IWebDriver driver, string frontendBaseUrl) =>
        LoginAsAdmin(driver, frontendBaseUrl);
}
