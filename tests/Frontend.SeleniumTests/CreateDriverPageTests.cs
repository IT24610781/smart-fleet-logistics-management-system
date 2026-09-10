using OpenQA.Selenium;

namespace Frontend.SeleniumTests;

/// <summary>
/// US-01: Admin Creates User Account
/// End-to-end browser tests driving the real /admin/drivers/create
/// page against the real backend.
///
/// Uses AdminSessionInjector to simulate an Admin session, since there
/// is no seeded Admin account or Admin login path anywhere in the app
/// yet (a real gap — see AdminSessionInjector.cs for details). Every
/// other part of these tests exercises real app code: real routing,
/// real form validation, real API calls, real database writes.
///
/// Shares "Selenium collection" with the other browser-test classes so
/// xUnit runs them serially — only one real Chrome/ChromeDriver session
/// exists at a time. Without this, xUnit parallelizes across classes by
/// default, which launches several real browsers simultaneously and
/// starves them of CPU, causing WebDriverException timeouts that look
/// like app/network failures but are actually resource contention.
/// </summary>
[Collection("Selenium collection")]
public class CreateDriverPageTests : SeleniumTestBase
{
    private void LoginAsAdmin()
    {
        AdminSessionInjector.LoginAsAdminViaLocalStorage(Driver, FrontendBaseUrl);
    }

    private void FillDriverForm(
        string firstName, string lastName, string username,
        string email, string password, string contactNumber)
    {
        Driver.FindElement(By.Id("firstName")).SendKeys(firstName);
        Driver.FindElement(By.Id("lastName")).SendKeys(lastName);
        Driver.FindElement(By.Id("username")).SendKeys(username);
        Driver.FindElement(By.Id("email")).SendKeys(email);
        Driver.FindElement(By.Id("password")).SendKeys(password);
        Driver.FindElement(By.Id("contactNumber")).SendKeys(contactNumber);
    }

    // ============================================================
    // Scenario 1 - Successful account creation
    // ============================================================

    [Fact]
    public void CreateDriver_ValidDetails_ShowsSuccessMessage()
    {
        LoginAsAdmin();
        Driver.Navigate().GoToUrl($"{FrontendBaseUrl}/admin/drivers/create");

        Wait.Until(d => d.FindElement(By.Id("firstName")));

        var suffix = Guid.NewGuid().ToString("N")[..8];
        FillDriverForm(
            firstName: "Selenium",
            lastName: "CreatedDriver",
            username: $"seldrv{suffix}",
            email: $"seldrv{suffix}@example.com",
            password: "ValidPass123",
            contactNumber: "0770000000");

        Driver.FindElement(By.CssSelector("button[type='submit']")).Click();

        var successBanner = Wait.Until(d => d.FindElement(By.CssSelector(".success-banner")));
        Assert.Contains("Driver account created successfully.", successBanner.Text);
    }

    // ============================================================
    // Scenario 2 - Duplicate email
    // ============================================================

    [Fact]
    public void CreateDriver_DuplicateEmail_ShowsErrorAndDoesNotCreateSecondAccount()
    {
        LoginAsAdmin();
        Driver.Navigate().GoToUrl($"{FrontendBaseUrl}/admin/drivers/create");
        Wait.Until(d => d.FindElement(By.Id("firstName")));

        var suffix = Guid.NewGuid().ToString("N")[..8];
        var email = $"dup{suffix}@example.com";

        FillDriverForm("Selenium", "First", $"seldup1{suffix}", email, "ValidPass123", "0770000000");
        Driver.FindElement(By.CssSelector("button[type='submit']")).Click();
        Wait.Until(d => d.FindElement(By.CssSelector(".success-banner")));

        // Reload the empty form and try again with the SAME email,
        // different username.
        Driver.Navigate().GoToUrl($"{FrontendBaseUrl}/admin/drivers/create");
        Wait.Until(d => d.FindElement(By.Id("firstName")));

        FillDriverForm("Selenium", "Second", $"seldup2{suffix}", email, "ValidPass123", "0770000001");
        Driver.FindElement(By.CssSelector("button[type='submit']")).Click();

        var errorBanner = Wait.Until(d => d.FindElement(By.CssSelector(".error-banner")));
        Assert.Contains("already exists", errorBanner.Text, StringComparison.OrdinalIgnoreCase);

        var successBanners = Driver.FindElements(By.CssSelector(".success-banner"));
        Assert.Empty(successBanners);
    }

    // ============================================================
    // Scenario 3 - Invalid or missing fields
    // ============================================================

    [Fact]
    public void CreateDriver_MissingEmail_ShowsInlineError()
    {
        LoginAsAdmin();
        Driver.Navigate().GoToUrl($"{FrontendBaseUrl}/admin/drivers/create");
        Wait.Until(d => d.FindElement(By.Id("firstName")));

        var suffix = Guid.NewGuid().ToString("N")[..8];
        // Deliberately leave email blank.
        Driver.FindElement(By.Id("firstName")).SendKeys("Selenium");
        Driver.FindElement(By.Id("lastName")).SendKeys("NoEmail");
        Driver.FindElement(By.Id("username")).SendKeys($"selnoemail{suffix}");
        Driver.FindElement(By.Id("password")).SendKeys("ValidPass123");
        Driver.FindElement(By.Id("contactNumber")).SendKeys("0770000000");

        Driver.FindElement(By.CssSelector("button[type='submit']")).Click();

        var inlineError = Wait.Until(d => d.FindElement(By.Id("email-error")));
        Assert.Contains("valid email", inlineError.Text, StringComparison.OrdinalIgnoreCase);

        var successBanners = Driver.FindElements(By.CssSelector(".success-banner"));
        Assert.Empty(successBanners);
    }

    [Fact]
    public void CreateDriver_PasswordShorterThan8Chars_ShowsInlineError()
    {
        LoginAsAdmin();
        Driver.Navigate().GoToUrl($"{FrontendBaseUrl}/admin/drivers/create");
        Wait.Until(d => d.FindElement(By.Id("firstName")));

        var suffix = Guid.NewGuid().ToString("N")[..8];
        FillDriverForm(
            "Selenium", "ShortPass", $"selshort{suffix}",
            $"selshort{suffix}@example.com", "short1", "0770000000");

        Driver.FindElement(By.CssSelector("button[type='submit']")).Click();

        var inlineError = Wait.Until(d => d.FindElement(By.Id("password-error")));
        Assert.Contains("at least 8 characters", inlineError.Text);
    }

    // ============================================================
    // ⚠ KNOWN GAP vs Acceptance Criteria — documented, not hidden.
    // ============================================================

    [Fact]
    public void CreateDriver_RoleFieldIsReadOnlyAndAlwaysDriver()
    {
        // The AC says the Admin should "select a role", but the Role
        // field on this real page is a hardcoded, read-only "Driver"
        // value — there is no way to choose Dispatcher or Admin here.
        // This documents the CURRENT UI, matching the backend gap
        // already flagged for US-01's API-level tests.
        LoginAsAdmin();
        Driver.Navigate().GoToUrl($"{FrontendBaseUrl}/admin/drivers/create");

        var roleField = Wait.Until(d => d.FindElement(By.Id("role")));
        Assert.Equal("Driver", roleField.GetAttribute("value"));
        Assert.NotNull(roleField.GetAttribute("readonly"));
    }
}
