using OpenQA.Selenium;
using Xunit;

namespace Frontend.SeleniumTests;

/// <summary>US-19 Scenario 4: people who are not allowed to see the Driver Performance report are turned away.</summary>
[Collection("Sprint4 live")]
public class Us19AccessTests : Sprint4BrowserHelpers
{
    [Sprint3BrowserFact]
    [Trait("Category", "Sprint4Live")]
    [Trait("Story", "US-19")]
    [Trait("Scenario", "S4")]
    public Task TC19_09_Driver_IsBlockedFromTheReportsPage() => Run("TC19-09", async () =>
    {
        var s = await CreateTripAsync(accept: true);
        LoginFresh(s.Driver.Username, Pw);
        Go("/admin/reports");

        Wait.Until(d => d.Url.Contains("/403"));
        Assert.DoesNotContain("Driver Performance Report", Driver.FindElement(By.TagName("body")).Text);
        await Task.CompletedTask;
    });

    [Sprint3BrowserFact]
    [Trait("Category", "Sprint4Live")]
    [Trait("Story", "US-19")]
    [Trait("Scenario", "S4")]
    public Task TC19_10_NotLoggedIn_IsRedirectedToLogin() => Run("TC19-10", async () =>
    {
        Go("/login");
        ((IJavaScriptExecutor)Driver).ExecuteScript("localStorage.clear()");
        Go("/admin/reports");

        Wait.Until(d => d.Url.Contains("/login"));
        await Task.CompletedTask;
    });
}
