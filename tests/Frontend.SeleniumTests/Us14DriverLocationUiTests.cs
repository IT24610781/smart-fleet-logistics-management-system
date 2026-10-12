using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using Xunit;

namespace Frontend.SeleniumTests;

/// <summary>
/// SCRUM-154 / US-14: "Driver sends live location update", seen from the driver's My Trips page (/driver/trips).
/// The page shares the driver's browser position automatically while a trip is In-Progress.
/// Chrome is given a fake position through the DevTools protocol, so no real GPS is needed.
///   dotnet test tests/Frontend.SeleniumTests --filter "Story=US-14"
/// Same environment variables as TrackingPageTests (see Sprint4LiveBase.cs).
/// </summary>
[Collection("Sprint4 live")]
public class Us14DriverLocationUiTests : Sprint4LiveBase
{
    private const double FakeLat = 6.9271;
    private const double FakeLng = 79.8612;

    private ChromeDriver Chrome => (ChromeDriver)Driver;

    private void AllowAndFakeLocation()
    {
        Chrome.ExecuteCdpCommand("Browser.grantPermissions", new Dictionary<string, object>
        {
            ["origin"] = new Uri(FrontendBaseUrl).GetLeftPart(UriPartial.Authority),
            ["permissions"] = new[] { "geolocation" }
        });
        Chrome.ExecuteCdpCommand("Emulation.setGeolocationOverride", new Dictionary<string, object>
        {
            ["latitude"] = FakeLat,
            ["longitude"] = FakeLng,
            ["accuracy"] = 10
        });
    }

    private bool BodyContains(string text) =>
        Driver.FindElement(By.TagName("body")).Text.Contains(text, StringComparison.OrdinalIgnoreCase);

    [Sprint3BrowserFact]
    [Trait("Category", "Sprint4Live")]
    [Trait("Story", "US-14")]
    public Task TC14_01_InProgressTrip_ShowsSharingBannerAndSendsTheBrowserPosition() => Run("TC14-01", async () =>
    {
        var s = await CreateTripAsync(accept: true);
        LoginFresh(s.Driver.Username, Pw);
        AllowAndFakeLocation();
        Go("/driver/trips");

        Wait.Until(_ => BodyContains("Sharing your location automatically"));

        var pings = await WaitForPingsAsync(s.TripId, s.DispatcherToken, atLeast: 1);
        Assert.True(pings.Count >= 1, "The driver page never sent a location to the tracking service.");
        var first = pings[0]!;
        Assert.InRange(first["latitude"]!.GetValue<decimal>(), (decimal)FakeLat - 0.001m, (decimal)FakeLat + 0.001m);
        Assert.InRange(first["longitude"]!.GetValue<decimal>(), (decimal)FakeLng - 0.001m, (decimal)FakeLng + 0.001m);
    });

    [Sprint3BrowserFact]
    [Trait("Category", "Sprint4Live")]
    [Trait("Story", "US-14")]
    public Task TC14_02_TripNotYetAccepted_NoBannerAndNoLocationSent() => Run("TC14-02", async () =>
    {
        var s = await CreateTripAsync(accept: false); // stays Assigned
        LoginFresh(s.Driver.Username, Pw);
        AllowAndFakeLocation();
        Go("/driver/trips");

        Wait.Until(_ => BodyContains(s.Pickup)); // the assigned trip is listed, so the page has loaded
        await Task.Delay(TimeSpan.FromSeconds(6)); // long enough for the first automatic send to have happened

        Assert.False(BodyContains("Sharing your location automatically"));
        var pings = await WaitForPingsAsync(s.TripId, s.DispatcherToken, atLeast: 1, seconds: 1);
        Assert.Empty(pings);
    });

    [Sprint3BrowserFact]
    [Trait("Category", "Sprint4Live")]
    [Trait("Story", "US-14")]
    public Task TC14_03_BrowserWithoutLocationServices_ShowsAnExplanation() => Run("TC14-03", async () =>
    {
        var s = await CreateTripAsync(accept: true);

        // Hide navigator.geolocation on every new page, as on a browser that has no location support.
        Chrome.ExecuteCdpCommand("Page.addScriptToEvaluateOnNewDocument", new Dictionary<string, object>
        {
            ["source"] = "Object.defineProperty(Navigator.prototype, 'geolocation', { get: () => undefined });"
        });

        LoginFresh(s.Driver.Username, Pw);
        Go("/driver/trips");

        Wait.Until(_ => BodyContains("does not support location services"));
        Assert.False(BodyContains("Sharing your location automatically"));
    });

    [Sprint3BrowserFact]
    [Trait("Category", "Sprint4Live")]
    [Trait("Story", "US-14")]
    public Task TC14_04_DispatcherCannotOpenTheDriverTripsPage() => Run("TC14-04", async () =>
    {
        var s = await CreateTripAsync(accept: true);
        LoginFresh(s.DispatcherUser, Pw);
        Go("/driver/trips");

        Wait.Until(d => d.Url.Contains("/403"));
        await Task.CompletedTask;
    });

    [Sprint3BrowserFact]
    [Trait("Category", "Sprint4Live")]
    [Trait("Story", "US-14")]
    public Task TC14_05_NotLoggedIn_IsRedirectedToLogin() => Run("TC14-05", async () =>
    {
        Go("/login");
        ((IJavaScriptExecutor)Driver).ExecuteScript("localStorage.clear()");
        Go("/driver/trips");

        Wait.Until(d => d.Url.Contains("/login"));
        await Task.CompletedTask;
    });
}
