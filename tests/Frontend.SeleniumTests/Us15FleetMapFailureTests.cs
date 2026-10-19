using OpenQA.Selenium;
using Xunit;

namespace Frontend.SeleniumTests;

/// <summary>US-15 Scenario 4: the fleet map cannot load its data and must say so.</summary>
[Collection("Sprint4 live")]
public class Us15FleetMapFailureTests : Sprint4BrowserHelpers
{
    [Sprint3BrowserFact]
    [Trait("Category", "Sprint4Live")]
    [Trait("Story", "US-15")]
    [Trait("Scenario", "S4")]
    public Task TC15_07_FleetLocationsUnavailable_ShowsAnErrorMessage() => Run("TC15-07", async () =>
    {
        var s = await CreateTripAsync(accept: true);
        LoginFresh(s.DispatcherUser, Pw);
        BlockRequests("/api/tracking/fleet/latest");
        Go("/dispatcher/tracking");

        Wait.Until(d => d.FindElements(By.TagName("h1")).Any(h => h.Text.Contains("Fleet Tracking")));
        Wait.Until(_ => AlertIsShown());
        await Task.CompletedTask;
    });
}
