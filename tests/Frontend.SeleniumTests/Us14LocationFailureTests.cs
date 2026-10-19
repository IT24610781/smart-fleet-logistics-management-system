using OpenQA.Selenium;
using Xunit;

namespace Frontend.SeleniumTests;

/// <summary>US-14 Scenario 4: the location cannot be obtained or sent, and the page must not claim success.</summary>
[Collection("Sprint4 live")]
public class Us14LocationFailureTests : Sprint4BrowserHelpers
{
    [Sprint3BrowserFact]
    [Trait("Category", "Sprint4Live")]
    [Trait("Story", "US-14")]
    [Trait("Scenario", "S4")]
    public Task TC14_06_LocationPermissionDenied_ShowsMessageAndNoSharingBanner() => Run("TC14-06", async () =>
    {
        var s = await CreateTripAsync(accept: true);
        LoginFresh(s.Driver.Username, Pw);
        DenyLocation();
        Go("/driver/trips");

        Wait.Until(_ => BodyContains("Could not get your current location"));
        Assert.False(BodyContains("Sharing your location automatically"),
            "A failed location lookup must not be shown as successful sharing.");
        var pings = await WaitForPingsAsync(s.TripId, s.DispatcherToken, atLeast: 1, seconds: 1);
        Assert.Empty(pings);
    });

    [Sprint3BrowserFact]
    [Trait("Category", "Sprint4Live")]
    [Trait("Story", "US-14")]
    [Trait("Scenario", "S4")]
    public Task TC14_07_TrackingServiceUnreachable_ShowsErrorAndNoSharingBanner() => Run("TC14-07", async () =>
    {
        var s = await CreateTripAsync(accept: true);
        LoginFresh(s.Driver.Username, Pw);
        FakeLocation();
        BlockRequests("/api/tracking/locations");
        Go("/driver/trips");

        Wait.Until(_ => AlertIsShown());
        Assert.False(BodyContains("Sharing your location automatically"),
            "A location that could not be transmitted must not be shown as successfully submitted.");
        var pings = await WaitForPingsAsync(s.TripId, s.DispatcherToken, atLeast: 1, seconds: 1);
        Assert.Empty(pings);
    });
}
