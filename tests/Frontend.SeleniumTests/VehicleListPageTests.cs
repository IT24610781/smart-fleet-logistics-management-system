using OpenQA.Selenium;

namespace Frontend.SeleniumTests;

[Collection("Selenium collection")]
public class VehicleListPageTests : SeleniumTestBase
{
    private void LoginAsAdmin()
    {
        AdminSessionInjector.LoginAsAdminViaLocalStorage(Driver, FrontendBaseUrl);
    }

    private string CreateVehicleViaUi(string type = "Van", string capacity = "6")
    {
        var regNo = $"LST-{Guid.NewGuid().ToString("N")[..8]}";

        Driver.Navigate().GoToUrl($"{FrontendBaseUrl}/admin/vehicles/create");
        Wait.Until(d => d.FindElement(By.Id("registrationNo")));
        Driver.FindElement(By.Id("registrationNo")).SendKeys(regNo);
        Driver.FindElement(By.Id("type")).SendKeys(type);
        Driver.FindElement(By.Id("capacity")).SendKeys(capacity);
        Driver.FindElement(By.CssSelector("button[type='submit']")).Click();
        Wait.Until(d => d.FindElement(By.CssSelector("[role='status']")));

        return regNo;
    }

    [Fact]
    public void GetVehicles_ShowsCreatedVehicleInTable()
    {
        LoginAsAdmin();
        var regNo = CreateVehicleViaUi();

        Driver.Navigate().GoToUrl($"{FrontendBaseUrl}/admin/vehicles");

        var table = Wait.Until(d =>
        {
            var el = d.FindElements(By.CssSelector("table"));
            return el.Count > 0 && el[0].Text.Contains(regNo) ? el[0] : null;
        });

        Assert.NotNull(table);
        Assert.Contains(regNo, table!.Text);
    }

    [Fact]
    public void GetVehicles_SearchByRegistrationNo_FiltersTable()
    {
        LoginAsAdmin();
        var regNo = CreateVehicleViaUi();
        var otherRegNo = CreateVehicleViaUi();

        Driver.Navigate().GoToUrl($"{FrontendBaseUrl}/admin/vehicles");
        Wait.Until(d => d.FindElement(By.CssSelector("table")).Text.Contains(regNo));

        var searchBox = Driver.FindElement(By.CssSelector(".search-input"));
        searchBox.SendKeys(regNo);

        Wait.Until(d =>
        {
            var text = d.FindElement(By.CssSelector("table")).Text;
            return text.Contains(regNo) && !text.Contains(otherRegNo);
        });
    }

    [Fact]
    public void GetVehicles_FilterByStatus_ShowsOnlyMatchingStatus()
    {
        LoginAsAdmin();
        var regNo = CreateVehicleViaUi();

        Driver.Navigate().GoToUrl($"{FrontendBaseUrl}/admin/vehicles");
        Wait.Until(d => d.FindElement(By.CssSelector("table")).Text.Contains(regNo));

        var statusFilter = Driver.FindElements(By.CssSelector(".select-input"))
            .First(e => e.TagName == "select" && e.FindElements(By.TagName("option")).Any(o => o.Text == "All statuses"));

        var select = new OpenQA.Selenium.Support.UI.SelectElement(statusFilter);
        select.SelectByValue("available");

        Wait.Until(d => d.FindElement(By.CssSelector("table")).Text.Contains(regNo));
    }
}