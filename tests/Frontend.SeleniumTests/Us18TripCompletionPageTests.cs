using System.Globalization;
using OpenQA.Selenium;
using OpenQA.Selenium.Support.UI;
using Xunit;

namespace Frontend.SeleniumTests;

/// <summary>
/// SCRUM-154 / US-18: Admin generates the Trip Completion Report on /admin/reports (plus the Trip Details table below it).
///   dotnet test tests/Frontend.SeleniumTests --filter "Story=US-18"
/// Same environment variables as TrackingPageTests (see Sprint4LiveBase.cs).
/// The report rules (from TripCompletionService): eligible = every trip created in the period, whatever its status;
/// completion rate = completed / eligible * 100, rounded to 2 decimals. TC18_02 and TC18_03 check those rules.
/// </summary>
[Collection("Sprint4 live")]
public class Us18TripCompletionPageTests : Sprint4LiveBase
{
    private const string Title = "Trip Completion Report";

    private IWebElement OpenCard()
    {
        OpenReportsAsAdmin();
        var card = Card(Title);
        WaitUntilLoaded(card);
        return card;
    }

    [Sprint3BrowserFact]
    [Trait("Category", "Sprint4Live")]
    [Trait("Story", "US-18")]
    public Task TC18_01_ReportShowsEverySummaryLine() => Run("TC18-01", async () =>
    {
        await CreateTripAsync(accept: true);
        var card = OpenCard();

        foreach (var label in new[] { "Eligible trips", "Completed", "In progress", "Assigned", "Requested", "Cancelled", "Completion rate" })
        {
            Wait.Until(_ => card.FindElements(By.XPath(
                $".//div[contains(@class,'report-metric-row')][span[1][normalize-space()='{label}']]")).Count > 0);
        }
        await Task.CompletedTask;
    });

    [Sprint3BrowserFact]
    [Trait("Category", "Sprint4Live")]
    [Trait("Story", "US-18")]
    public Task TC18_02_EligibleTrips_EqualsTheSumOfTheFiveStatusCounts() => Run("TC18-02", async () =>
    {
        await CreateTripAsync(accept: true);
        await CreateTripAsync(accept: false);
        var card = OpenCard();

        var eligible = Metric(card, "Eligible trips");
        var sum = Metric(card, "Completed") + Metric(card, "In progress") + Metric(card, "Assigned")
                  + Metric(card, "Requested") + Metric(card, "Cancelled");
        Assert.Equal(eligible, sum);
        await Task.CompletedTask;
    });

    [Sprint3BrowserFact]
    [Trait("Category", "Sprint4Live")]
    [Trait("Story", "US-18")]
    public Task TC18_03_CompletionRate_IsCompletedDividedByEligibleRoundedToTwoDecimals() => Run("TC18-03", async () =>
    {
        var done = await CreateTripAsync(accept: true);
        await CompleteTripAsync(done);
        await CreateTripAsync(accept: true);
        var card = OpenCard();

        var eligible = Metric(card, "Eligible trips");
        var completed = Metric(card, "Completed");
        var expected = Math.Round((double)completed / eligible * 100, 2).ToString("0.##", CultureInfo.InvariantCulture);

        var shown = card.FindElement(By.XPath(
            ".//div[contains(@class,'report-metric-row')][span[1][normalize-space()='Completion rate']]/span[2]")).Text.Trim();
        Assert.Equal(expected + "%", shown);
        await Task.CompletedTask;
    });

    [Sprint3BrowserFact]
    [Trait("Category", "Sprint4Live")]
    [Trait("Story", "US-18")]
    public Task TC18_04_CompletingATrip_MovesItFromInProgressToCompletedAfterRefresh() => Run("TC18-04", async () =>
    {
        var s = await CreateTripAsync(accept: true);
        var card = OpenCard();
        var completedBefore = Metric(card, "Completed");
        var inProgressBefore = Metric(card, "In progress");
        var eligibleBefore = Metric(card, "Eligible trips");

        await CompleteTripAsync(s);
        ClickRefresh(card);

        WaitForMetric(card, "Completed", completedBefore + 1);
        Assert.Equal(inProgressBefore - 1, Metric(card, "In progress"));
        Assert.Equal(eligibleBefore, Metric(card, "Eligible trips")); // moving status does not change the total
    });

    [Sprint3BrowserFact]
    [Trait("Category", "Sprint4Live")]
    [Trait("Story", "US-18")]
    public Task TC18_05_NewTrip_RaisesEligibleAndRequestedAfterRefresh() => Run("TC18-05", async () =>
    {
        var seed = await CreateTripAsync(accept: true);
        var card = OpenCard();
        var eligibleBefore = Metric(card, "Eligible trips");
        var requestedBefore = Metric(card, "Requested");

        await Api(TripUrl, "/api/trips", "POST", new
        {
            pickupLocation = "QA S4 extra " + Guid.NewGuid().ToString("N")[..6],
            dropoffLocation = "QA S4 Destination",
            requiredCapacity = "700001 persons"
        }, seed.DispatcherToken);
        ClickRefresh(card);

        WaitForMetric(card, "Eligible trips", eligibleBefore + 1);
        Assert.Equal(requestedBefore + 1, Metric(card, "Requested"));
    });

    [Sprint3BrowserFact]
    [Trait("Category", "Sprint4Live")]
    [Trait("Story", "US-18")]
    public Task TC18_06_TripDetails_StatusFilterShowsAndHidesTheTrip() => Run("TC18-06", async () =>
    {
        var s = await CreateTripAsync(accept: true); // In-Progress
        OpenReportsAsAdmin();
        var details = Card("Trip Details");
        WaitUntilLoaded(details);

        var filter = new SelectElement(details.FindElement(By.TagName("select")));
        By tripButton = By.CssSelector($"button[aria-label='View trip {s.TripId}']");

        filter.SelectByText("In-Progress");
        Wait.Until(d => d.FindElements(tripButton).Count > 0);

        filter.SelectByText("Completed");
        Wait.Until(d => d.FindElements(tripButton).Count == 0);

        filter.SelectByText("All statuses");
        Wait.Until(d => d.FindElements(tripButton).Count > 0);
        await Task.CompletedTask;
    });

    [Sprint3BrowserFact]
    [Trait("Category", "Sprint4Live")]
    [Trait("Story", "US-18")]
    public Task TC18_07_TripDetails_ClickingATripShowsItsPickupAndDriver() => Run("TC18-07", async () =>
    {
        var s = await CreateTripAsync(accept: true);
        OpenReportsAsAdmin();
        var details = Card("Trip Details");
        WaitUntilLoaded(details);

        Wait.Until(d => d.FindElements(By.CssSelector($"button[aria-label='View trip {s.TripId}']")).FirstOrDefault())!.Click();

        Wait.Until(d => d.FindElement(By.TagName("body")).Text.Contains("Trip details"));
        Wait.Until(d => d.FindElement(By.TagName("body")).Text.Contains(s.Pickup));
        Assert.Contains(s.Driver.LastName, Driver.FindElement(By.TagName("body")).Text);
        await Task.CompletedTask;
    });

    [Sprint3BrowserFact]
    [Trait("Category", "Sprint4Live")]
    [Trait("Story", "US-18")]
    public Task TC18_08_ChangingTheDateRange_ReloadsTheReportWithoutAnError() => Run("TC18-08", async () =>
    {
        await CreateTripAsync(accept: true);
        var card = OpenCard();
        var eligible7 = Metric(card, "Eligible trips");

        new SelectElement(Driver.FindElement(By.CssSelector("select.select-input"))).SelectByText("Last 90 days");

        Wait.Until(_ => (TryMetric(card, "Eligible trips") ?? -1) >= eligible7); // a longer period never has fewer trips
        Assert.DoesNotContain("Could not load", card.Text);
        await Task.CompletedTask;
    });
}
