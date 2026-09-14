using OpenQA.Selenium;

namespace Frontend.SeleniumTests;

[Collection("Selenium collection")]
public class VehicleDeletePageTests : SeleniumTestBase
{
    private void LoginAsAdmin()
    {
        AdminSessionInjector.LoginAsAdminViaLocalStorage(Driver, FrontendBaseUrl);
    }

    private string CreateVehicleViaUi(string type = "Van", string capacity = "6")
    {
        var regNo = $"DEL-{Guid.NewGuid().ToString("N")[..8]}";

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
    public void DeleteVehicle_ConfirmDelete_RemovesFromTable()
    {
        LoginAsAdmin();
        var regNo = CreateVehicleViaUi();

        Driver.Navigate().GoToUrl($"{FrontendBaseUrl}/admin/vehicles");
        Wait.Until(d => d.FindElement(By.CssSelector("table")).Text.Contains(regNo));

        Driver.FindElement(By.CssSelector($"button[aria-label='Delete {regNo}']")).Click();

        var dialog = Wait.Until(d => d.FindElement(By.CssSelector("[role='dialog']")));
        Assert.Contains("Remove vehicle?", dialog.Text);
        Assert.Contains(regNo, dialog.Text);

        Driver.FindElement(By.XPath("//div[@role='dialog']//button[normalize-space()='Remove']")).Click();

        Wait.Until(d => d.FindElements(By.CssSelector("[role='dialog']")).Count == 0);
        Wait.Until(d => !d.FindElement(By.CssSelector("table")).Text.Contains(regNo));
    }

    [Fact]
    public void DeleteVehicle_CancelDelete_KeepsVehicleInTable()
    {
        LoginAsAdmin();
        var regNo = CreateVehicleViaUi();

        Driver.Navigate().GoToUrl($"{FrontendBaseUrl}/admin/vehicles");
        Wait.Until(d => d.FindElement(By.CssSelector("table")).Text.Contains(regNo));

        Driver.FindElement(By.CssSelector($"button[aria-label='Delete {regNo}']")).Click();
        Wait.Until(d => d.FindElement(By.CssSelector("[role='dialog']")));

        Driver.FindElement(By.XPath("//div[@role='dialog']//button[normalize-space()='Cancel']")).Click();

        Wait.Until(d => d.FindElements(By.CssSelector("[role='dialog']")).Count == 0);
        Assert.Contains(regNo, Driver.FindElement(By.CssSelector("table")).Text);
    }
}