using System.Globalization;
using System.Text.RegularExpressions;
using OpenQA.Selenium;
using OpenQA.Selenium.Support.UI;
using Xunit;

namespace Frontend.SeleniumTests;

/// <summary>
/// SCRUM-154 / US-17: Admin generates the Fleet Utilization Report on /admin/reports.
///   dotnet test tests/Frontend.SeleniumTests --filter "Story=US-17"
/// Same environment variables as TrackingPageTests (see Sprint4LiveBase.cs).
/// Two tests check report invariants (summary count = table rows, percentages within 0 to 100);
/// if one fails, it is a bug candidate in the report logic, not in the test.
/// </summary>
[Collection("Sprint4 live")]
public class Us17FleetUtilizationPageTests : Sprint4LiveBase
{
    private const string Title = "Fleet Utilization Report";

    private IWebElement OpenCard()
    {
        OpenReportsAsAdmin();
        var card = Card(Title);
        WaitUntilLoaded(card);
        return card;
    }

    private static IReadOnlyCollection<IWebElement> DataRows(IWebElement card) =>
        card.FindElements(By.CssSelector("table.utilization-table tbody tr"));

    [Sprint3BrowserFact]
    [Trait("Category", "Sprint4Live")]
    [Trait("Story", "US-17")]
    public Task TC17_01_NewVehicle_AppearsInTheReportWithItsType() => Run("TC17-01", async () =>
    {
        var s = await CreateTripAsync(accept: true);
        var card = OpenCard();

        var row = Wait.Until(_ => card.FindElements(By.XPath(
            $".//table[contains(@class,'utilization-table')]//tr[td[contains(normalize-space(.),'{s.VehicleReg}')]]"))
            .FirstOrDefault())!;
        Assert.Contains("Van", row.Text);
        Assert.Contains("%", row.Text);
    });

    [Sprint3BrowserFact]
    [Trait("Category", "Sprint4Live")]
    [Trait("Story", "US-17")]
    public Task TC17_02_SummaryShowsAllThreeFleetMetrics() => Run("TC17-02", async () =>
    {
        await CreateTripAsync(accept: true);
        var card = OpenCard();

        Wait.Until(_ => card.Text.Contains("Vehicles in report", StringComparison.OrdinalIgnoreCase));
        foreach (var expected in new[] { "Fleet-wide utilization", "Total in-use hours", "In-use hours", "Trips", "Utilization" })
        {
            // Case-insensitive: table headers may be shown in capitals by the stylesheet.
            Assert.Contains(expected, card.Text, StringComparison.OrdinalIgnoreCase);
        }
    });

    [Sprint3BrowserFact]
    [Trait("Category", "Sprint4Live")]
    [Trait("Story", "US-17")]
    public Task TC17_03_VehiclesInReportMatchesTheNumberOfTableRows() => Run("TC17-03", async () =>
    {
        await CreateTripAsync(accept: true);
        var card = OpenCard();

        var reported = Metric(card, "Vehicles in report");
        Assert.Equal(reported, DataRows(card).Count);
    });

    [Sprint3BrowserFact]
    [Trait("Category", "Sprint4Live")]
    [Trait("Story", "US-17")]
    public Task TC17_04_EveryUtilizationPercentage_IsBetween0And100() => Run("TC17-04", async () =>
    {
        await CreateTripAsync(accept: true);
        var card = OpenCard();

        var rows = Wait.Until(_ => { var r = DataRows(card); return r.Count > 0 ? r : null; })!;
        foreach (var row in rows)
        {
            var cells = row.FindElements(By.TagName("td"));
            var match = Regex.Match(cells[3].Text, @"^\s*(-?[0-9]+(?:\.[0-9]+)?)\s*%");
            Assert.True(match.Success, $"Could not read a percentage from '{cells[3].Text}' in row '{row.Text}'.");
            var percent = double.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
            Assert.InRange(percent, 0, 100);
        }
    });

    [Sprint3BrowserFact]
    [Trait("Category", "Sprint4Live")]
    [Trait("Story", "US-17")]
    public Task TC17_05_ChangingTheDateRange_ReloadsTheReportWithoutAnError() => Run("TC17-05", async () =>
    {
        var s = await CreateTripAsync(accept: true);
        var card = OpenCard();

        var range = new SelectElement(Driver.FindElement(By.CssSelector("select.select-input")));
        range.SelectByText("Last 90 days");

        Wait.Until(_ => card.FindElements(By.XPath($".//td[contains(normalize-space(.),'{s.VehicleReg}')]")).Count > 0);
        Assert.DoesNotContain("Could not load", card.Text);
        await Task.CompletedTask;
    });

    [Sprint3BrowserFact]
    [Trait("Category", "Sprint4Live")]
    [Trait("Story", "US-17")]
    public Task TC17_06_RefreshButton_ReloadsTheReportWithoutAnError() => Run("TC17-06", async () =>
    {
        var s = await CreateTripAsync(accept: true);
        var card = OpenCard();

        ClickRefresh(card);

        Wait.Until(_ => card.FindElements(By.XPath($".//td[contains(normalize-space(.),'{s.VehicleReg}')]")).Count > 0);
        Assert.DoesNotContain("Could not load", card.Text);
        await Task.CompletedTask;
    });

    [Sprint3BrowserFact]
    [Trait("Category", "Sprint4Live")]
    [Trait("Story", "US-17")]
    public Task TC17_07_Dispatcher_IsBlockedFromTheReportsPage() => Run("TC17-07", async () =>
    {
        var s = await CreateTripAsync(accept: true);
        LoginFresh(s.DispatcherUser, Pw);
        Go("/admin/reports");

        Wait.Until(d => d.Url.Contains("/403"));
        Assert.DoesNotContain("Fleet Utilization Report", Driver.FindElement(By.TagName("body")).Text);
        await Task.CompletedTask;
    });

    [Sprint3BrowserFact]
    [Trait("Category", "Sprint4Live")]
    [Trait("Story", "US-17")]
    public Task TC17_08_Driver_IsBlockedFromTheReportsPage() => Run("TC17-08", async () =>
    {
        var s = await CreateTripAsync(accept: true);
        LoginFresh(s.Driver.Username, Pw);
        Go("/admin/reports");

        Wait.Until(d => d.Url.Contains("/403"));
        await Task.CompletedTask;
    });

    [Sprint3BrowserFact]
    [Trait("Category", "Sprint4Live")]
    [Trait("Story", "US-17")]
    public Task TC17_09_NotLoggedIn_IsRedirectedToLogin() => Run("TC17-09", async () =>
    {
        Go("/login");
        ((IJavaScriptExecutor)Driver).ExecuteScript("localStorage.clear()");
        Go("/admin/reports");

        Wait.Until(d => d.Url.Contains("/login"));
        await Task.CompletedTask;
    });
}
