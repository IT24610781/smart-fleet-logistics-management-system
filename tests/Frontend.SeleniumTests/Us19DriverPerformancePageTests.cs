using OpenQA.Selenium;
using OpenQA.Selenium.Support.UI;
using Xunit;

namespace Frontend.SeleniumTests;

/// <summary>
/// SCRUM-154 / US-19: Admin generates the Driver Performance Report on /admin/reports.
///   dotnet test tests/Frontend.SeleniumTests --filter "Story=US-19"
/// Same environment variables as TrackingPageTests (see Sprint4LiveBase.cs).
/// Rules (from DriverPerformanceService): Accepted = the driver's trips that are In-Progress or Completed;
/// Completed = the driver's Completed trips; Rejected = trips the driver rejected (kept even after reassignment);
/// a driver with all three at 0 shows a "No activity" badge. Every test driver has a unique last name, so its row can be found.
/// </summary>
[Collection("Sprint4 live")]
public class Us19DriverPerformancePageTests : Sprint4LiveBase
{
    private const string Title = "Driver Performance Report";

    private IWebElement OpenCard()
    {
        OpenReportsAsAdmin();
        var card = Card(Title);
        WaitUntilLoaded(card);
        return card;
    }

    private static IWebElement? FindRow(IWebElement card, string lastName) =>
        card.FindElements(By.XPath($".//table//tr[td[1][contains(normalize-space(.),'{lastName}')]]")).FirstOrDefault();

    private IWebElement WaitForRow(IWebElement card, string lastName) =>
        Wait.Until(_ => FindRow(card, lastName))!;

    private static string Cell(IWebElement row, int columnIndex) =>
        row.FindElements(By.TagName("td"))[columnIndex].Text.Trim();

    // Column order: 0 Driver, 1 Accepted, 2 Completed, 3 Rejected, 4 Activity
    private static (string Accepted, string Completed, string Rejected) Counts(IWebElement row) =>
        (Cell(row, 1), Cell(row, 2), Cell(row, 3));

    private void WaitForCounts(IWebElement card, string lastName, string accepted, string completed, string rejected) =>
        Wait.Until(_ =>
        {
            var row = FindRow(card, lastName);
            return row != null && Counts(row) == (accepted, completed, rejected);
        });

    [Sprint3BrowserFact]
    [Trait("Category", "Sprint4Live")]
    [Trait("Story", "US-19")]
    public Task TC19_01_DriverWithNoTrips_ShowsZerosAndANoActivityBadge() => Run("TC19-01", async () =>
    {
        var driver = await CreateDriverAsync(await AdminTokenAsync());
        var card = OpenCard();

        var row = WaitForRow(card, driver.LastName);
        Assert.Equal(("0", "0", "0"), Counts(row));
        Assert.Contains("No activity", row.Text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Active", row.Text);
    });

    [Sprint3BrowserFact]
    [Trait("Category", "Sprint4Live")]
    [Trait("Story", "US-19")]
    public Task TC19_02_DriverWhoAcceptedATrip_ShowsAcceptedOneAndNoBadge() => Run("TC19-02", async () =>
    {
        var s = await CreateTripAsync(accept: true);
        var card = OpenCard();

        var row = WaitForRow(card, s.Driver.LastName);
        Assert.Equal(("1", "0", "0"), Counts(row));
        Assert.DoesNotContain("No activity", row.Text, StringComparison.OrdinalIgnoreCase);
        await Task.CompletedTask;
    });

    [Sprint3BrowserFact]
    [Trait("Category", "Sprint4Live")]
    [Trait("Story", "US-19")]
    public Task TC19_03_AfterTheTripIsCompleted_BothAcceptedAndCompletedShowOne() => Run("TC19-03", async () =>
    {
        var s = await CreateTripAsync(accept: true);
        await CompleteTripAsync(s);
        var card = OpenCard();

        var row = WaitForRow(card, s.Driver.LastName);
        Assert.Equal(("1", "1", "0"), Counts(row));
        await Task.CompletedTask;
    });

    [Sprint3BrowserFact]
    [Trait("Category", "Sprint4Live")]
    [Trait("Story", "US-19")]
    public Task TC19_04_DriverWhoRejectedATrip_ShowsRejectedOneAndAcceptedZero() => Run("TC19-04", async () =>
    {
        var s = await CreateTripAsync(accept: false);
        await RejectTripAsync(s);
        var card = OpenCard();

        var row = WaitForRow(card, s.Driver.LastName);
        Assert.Equal(("0", "0", "1"), Counts(row));
        Assert.DoesNotContain("No activity", row.Text, StringComparison.OrdinalIgnoreCase); // a rejection counts as activity
        await Task.CompletedTask;
    });

    [Sprint3BrowserFact]
    [Trait("Category", "Sprint4Live")]
    [Trait("Story", "US-19")]
    public Task TC19_05_DriverDropdown_FiltersToOneDriverAndBack() => Run("TC19-05", async () =>
    {
        var first = await CreateTripAsync(accept: true);
        var second = await CreateDriverAsync(await AdminTokenAsync());
        var card = OpenCard();
        WaitForRow(card, first.Driver.LastName);
        WaitForRow(card, second.LastName);

        var dropdown = new SelectElement(card.FindElement(By.TagName("select")));
        dropdown.SelectByText("QA " + first.Driver.LastName);

        Wait.Until(_ => card.FindElements(By.CssSelector("table tbody tr")).Count == 1);
        Assert.NotNull(FindRow(card, first.Driver.LastName));
        Assert.Null(FindRow(card, second.LastName));

        dropdown.SelectByText("All drivers");
        Wait.Until(_ => FindRow(card, first.Driver.LastName) != null && FindRow(card, second.LastName) != null);
        await Task.CompletedTask;
    });

    [Sprint3BrowserFact]
    [Trait("Category", "Sprint4Live")]
    [Trait("Story", "US-19")]
    public Task TC19_06_Refresh_PicksUpAnAcceptanceMadeAfterThePageLoaded() => Run("TC19-06", async () =>
    {
        var s = await CreateTripAsync(accept: false);
        var card = OpenCard();
        var row = WaitForRow(card, s.Driver.LastName);
        Assert.Equal(("0", "0", "0"), Counts(row));

        await AcceptTripAsync(s);
        ClickRefresh(card);

        WaitForCounts(card, s.Driver.LastName, "1", "0", "0");
        await Task.CompletedTask;
    });

    [Sprint3BrowserFact]
    [Trait("Category", "Sprint4Live")]
    [Trait("Story", "US-19")]
    public Task TC19_07_ReportShowsThePeriodAndTheNoRankingNote() => Run("TC19-07", async () =>
    {
        await CreateTripAsync(accept: true);
        var card = OpenCard();

        Assert.Contains("Last 7 days", card.Text);
        Assert.Contains("No combined score or ranking is shown", card.Text);
        await Task.CompletedTask;
    });

    [Sprint3BrowserFact]
    [Trait("Category", "Sprint4Live")]
    [Trait("Story", "US-19")]
    public Task TC19_08_Dispatcher_IsBlockedFromTheReportsPage() => Run("TC19-08", async () =>
    {
        var s = await CreateTripAsync(accept: true);
        LoginFresh(s.DispatcherUser, Pw);
        Go("/admin/reports");

        Wait.Until(d => d.Url.Contains("/403"));
        Assert.DoesNotContain("Driver Performance Report", Driver.FindElement(By.TagName("body")).Text);
        await Task.CompletedTask;
    });
}
