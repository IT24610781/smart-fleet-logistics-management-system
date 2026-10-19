using OpenQA.Selenium;

namespace Frontend.SeleniumTests;

[Collection("Selenium collection")]
public class VehicleCreatePageTests : SeleniumTestBase
{
    private void LoginAsAdmin()
    {
        AdminSessionInjector.LoginAsAdminViaLocalStorage(Driver, FrontendBaseUrl);
    }

    private void FillVehicleForm(string registrationNo, string type, string capacity)
    {
        Driver.FindElement(By.Id("registrationNo")).SendKeys(registrationNo);
        Driver.FindElement(By.Id("type")).SendKeys(type);
        Driver.FindElement(By.Id("capacity")).SendKeys(capacity);
    }

   [Fact]
    public void CreateVehicle_ValidDetails_ShowsSuccessMessage()
    {
    LoginAsAdmin();
    Driver.Navigate().GoToUrl($"{FrontendBaseUrl}/admin/vehicles/create");
    Wait.Until(d => d.FindElement(By.Id("registrationNo")));

    var suffix = Guid.NewGuid().ToString("N")[..8];
    FillVehicleForm($"SEL-{suffix}", "Van", "6");

    Driver.FindElement(By.CssSelector("button[type='submit']")).Click();

    Wait.Until(d =>
{
    var banner = d.FindElements(By.CssSelector("[role='status']"))
                   .FirstOrDefault();

    return banner != null &&
           banner.Text.Contains(
               "Vehicle added successfully.",
               StringComparison.OrdinalIgnoreCase);
});
    }

    [Fact]
    public void CreateVehicle_CapacityWithUnit_ShowsSuccessMessage()
    {
        LoginAsAdmin();
        Driver.Navigate().GoToUrl($"{FrontendBaseUrl}/admin/vehicles/create");
        Wait.Until(d => d.FindElement(By.Id("registrationNo")));

        var suffix = Guid.NewGuid().ToString("N")[..8];
        FillVehicleForm($"SEL-U{suffix}", "Van", "500 kg");

        Driver.FindElement(By.CssSelector("button[type='submit']")).Click();

        var successBanner = Wait.Until(d => d.FindElement(By.CssSelector("[role='status']")));
        Assert.Contains("Vehicle added successfully.", successBanner.Text);
    }

    [Fact]
    public void CreateVehicle_DuplicateRegistrationNo_ShowsErrorAndDoesNotCreateSecondRecord()
    {
        LoginAsAdmin();
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var regNo = $"DUP-{suffix}";

        Driver.Navigate().GoToUrl($"{FrontendBaseUrl}/admin/vehicles/create");
        Wait.Until(d => d.FindElement(By.Id("registrationNo")));
        FillVehicleForm(regNo, "Truck", "10");
        Driver.FindElement(By.CssSelector("button[type='submit']")).Click();
        Wait.Until(d => d.FindElement(By.CssSelector("[role='status']")));

        Driver.Navigate().GoToUrl($"{FrontendBaseUrl}/admin/vehicles/create");
        Wait.Until(d => d.FindElement(By.Id("registrationNo")));
        FillVehicleForm(regNo, "Bike", "2");
        Driver.FindElement(By.CssSelector("button[type='submit']")).Click();

        var errorBanner = Wait.Until(d => d.FindElement(By.CssSelector("[role='alert']")));
        Assert.Contains("already exists", errorBanner.Text, StringComparison.OrdinalIgnoreCase);

        Assert.Empty(Driver.FindElements(By.CssSelector("[role='status']")));
    }

    [Fact]
    public void CreateVehicle_MissingRegistrationNo_ShowsInlineError()
    {
        LoginAsAdmin();
        Driver.Navigate().GoToUrl($"{FrontendBaseUrl}/admin/vehicles/create");
        Wait.Until(d => d.FindElement(By.Id("registrationNo")));

        Driver.FindElement(By.Id("type")).SendKeys("Van");
        Driver.FindElement(By.Id("capacity")).SendKeys("5");
        Driver.FindElement(By.CssSelector("button[type='submit']")).Click();

        var inlineError = Wait.Until(d => d.FindElement(By.Id("registrationNo-error")));
        Assert.Contains("required", inlineError.Text, StringComparison.OrdinalIgnoreCase);

        Assert.Empty(Driver.FindElements(By.CssSelector("[role='status']")));
    }

    [Fact]
    public void CreateVehicle_InvalidCapacity_ShowsInlineError()
    {
        LoginAsAdmin();
        var suffix = Guid.NewGuid().ToString("N")[..8];

        Driver.Navigate().GoToUrl($"{FrontendBaseUrl}/admin/vehicles/create");
        Wait.Until(d => d.FindElement(By.Id("registrationNo")));
        FillVehicleForm($"CAP-{suffix}", "Van", "-3");
        Driver.FindElement(By.CssSelector("button[type='submit']")).Click();

        var inlineError = Wait.Until(d => d.FindElement(By.Id("capacity-error")));
        Assert.Contains("valid positive number", inlineError.Text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void CreateVehicle_StatusFieldIsReadOnlyAndDefaultsToAvailable()
    {
        LoginAsAdmin();
        Driver.Navigate().GoToUrl($"{FrontendBaseUrl}/admin/vehicles/create");

        var statusField = Wait.Until(d => d.FindElement(By.Id("status")));
        Assert.Equal("Available", statusField.GetAttribute("value"));
        Assert.NotNull(statusField.GetAttribute("readonly"));
    }
}