using OpenQA.Selenium;
using OpenQA.Selenium.Support.UI;

namespace Frontend.SeleniumTests;

[Collection("Selenium collection")]
public class VehicleUpdatePageTests : SeleniumTestBase
{
    private void LoginAsAdmin()
    {
        AdminSessionInjector.LoginAsAdminViaLocalStorage(Driver, FrontendBaseUrl);
    }

    private string CreateVehicleViaUi(string type = "Van", string capacity = "6")
    {
        var regNo = $"UPD-{Guid.NewGuid().ToString("N")[..8]}";

        Driver.Navigate().GoToUrl($"{FrontendBaseUrl}/admin/vehicles/create");
        Wait.Until(d => d.FindElement(By.Id("registrationNo")));
        Driver.FindElement(By.Id("registrationNo")).SendKeys(regNo);
        Driver.FindElement(By.Id("type")).SendKeys(type);
        Driver.FindElement(By.Id("capacity")).SendKeys(capacity);
        Driver.FindElement(By.CssSelector("button[type='submit']")).Click();
        Wait.Until(d => d.FindElement(By.CssSelector("[role='status']")));

        return regNo;
    }

    private void NavigateToEditViaListClick(string regNo)
    {
        Driver.Navigate().GoToUrl($"{FrontendBaseUrl}/admin/vehicles");
        Wait.Until(d => d.FindElement(By.CssSelector("table")).Text.Contains(regNo));

        Driver.FindElement(By.CssSelector($"a[aria-label='Edit {regNo}']")).Click();
        Wait.Until(d => d.Url.Contains("/edit"));
    }

    [Fact]
    public void UpdateVehicle_ValidChanges_ShowsSuccessMessage()
    {
        LoginAsAdmin();
        var regNo = CreateVehicleViaUi();
        NavigateToEditViaListClick(regNo);

        Wait.Until(d => d.FindElement(By.Id("type")));

        var typeField = Driver.FindElement(By.Id("type"));
        typeField.Clear();
        typeField.SendKeys("Truck");

        var capacityField = Driver.FindElement(By.Id("capacity"));
        capacityField.Clear();
        capacityField.SendKeys("12");

        Driver.FindElement(By.CssSelector("button[type='submit']")).Click();

        var successBanner = Wait.Until(d => d.FindElement(By.CssSelector("[role='status']")));
        Assert.Contains("Vehicle updated successfully.", successBanner.Text);
    }

    [Fact]
    public void UpdateVehicle_InvalidCapacity_ShowsInlineError()
    {
        LoginAsAdmin();
        var regNo = CreateVehicleViaUi();
        NavigateToEditViaListClick(regNo);

        Wait.Until(d => d.FindElement(By.Id("capacity")));
        var capacityField = Driver.FindElement(By.Id("capacity"));
        capacityField.Clear();
        capacityField.SendKeys("0");

        Driver.FindElement(By.CssSelector("button[type='submit']")).Click();

        var inlineError = Wait.Until(d => d.FindElement(By.Id("capacity-error")));
        Assert.Contains("valid positive number", inlineError.Text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void UpdateVehicle_RegistrationNumberFieldIsReadOnly()
    {
        LoginAsAdmin();
        var regNo = CreateVehicleViaUi();
        NavigateToEditViaListClick(regNo);

        var regField = Wait.Until(d => d.FindElement(By.Id("registrationNo")));
        Assert.Equal(regNo, regField.GetAttribute("value"));
        Assert.NotNull(regField.GetAttribute("readonly"));
    }

    // ============================================================
    // ⚠ KNOWN GAP — documented, not hidden.
    // EditVehicle.jsx reads location.state?.vehicle, which is only
    // populated when navigated to via clicking the Edit Link on the
    // Vehicles list page (React Router in-memory state). A direct
    // URL navigation to /admin/vehicles/{id}/edit — e.g. a page
    // refresh, a bookmarked link, or a shared URL — has no state,
    // so the page shows a "couldn't find" fallback instead of
    // fetching the vehicle by ID from the API. Recommend: fetch the
    // vehicle by ID from the API when state is missing, instead of
    // relying solely on router state.
    // ============================================================
    [Fact]
    public void EditVehiclePage_DirectUrlNavigationWithoutRouterState_ShowsNotFoundFallback_KnownGap()
    {
        LoginAsAdmin();
        var regNo = CreateVehicleViaUi();

        Driver.Navigate().GoToUrl($"{FrontendBaseUrl}/admin/vehicles");
        Wait.Until(d => d.FindElement(By.CssSelector("table")).Text.Contains(regNo));

        var editLink = Driver.FindElement(By.CssSelector($"a[aria-label='Edit {regNo}']"));
        var editUrl = editLink.GetAttribute("href");

        // Simulate a refresh/bookmark/shared-link scenario: navigate
        // directly to the URL instead of clicking the in-app Link.
        Driver.Navigate().GoToUrl(editUrl!);

        var pageText = Wait.Until(d => d.FindElement(By.TagName("body")));
        Assert.Contains("couldn't find that vehicle's details", pageText.Text, StringComparison.OrdinalIgnoreCase);
    }
}