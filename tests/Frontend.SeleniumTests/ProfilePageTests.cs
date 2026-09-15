using OpenQA.Selenium;

namespace Frontend.SeleniumTests;

/// <summary>
/// US-04: View and Edit Profile
/// End-to-end browser tests driving the real frontend (Vite dev
/// server) against the real backend (AuthUserService), via Chrome.
///
/// PREREQUISITES (see SeleniumTestBase for details):
///   - AuthUserService running at http://localhost:5000
///   - Frontend running at http://localhost:5173
///   - Chrome installed locally
///
/// A test driver account is seeded automatically once per test run
/// via SeededDriverFixture (see TestDataSeeder.cs) — you don't need to
/// create one manually.
/// </summary>
[Collection("Selenium collection")]
public class ProfilePageTests : SeleniumTestBase
{
    private readonly SeededDriverFixture _seeded;

    public ProfilePageTests(SeededDriverFixture seeded)
    {
        _seeded = seeded;
    }

    // ============================================================
    // Scenario 1 - View profile
    // ============================================================

    [Fact]
    public void ViewProfile_ShowsCurrentNameEmailAndContactNumber()
    {
        LoginAs(_seeded.Username, _seeded.Password);

        // Navigate via the real UI element, not a direct URL — this is
        // literally what the AC describes ("navigate to My Profile").
        Driver.FindElement(By.CssSelector(".topbar-user")).Click();

        Wait.Until(d => d.Url.Contains("/profile"));

        var firstName = Wait.Until(d => d.FindElement(By.Id("firstName")));
        var lastName = Driver.FindElement(By.Id("lastName"));
        var email = Driver.FindElement(By.Id("email"));
        var contactNumber = Driver.FindElement(By.Id("contactNumber"));

        Assert.Equal("Selenium", firstName.GetAttribute("value"));
        Assert.Equal("Tester", lastName.GetAttribute("value"));
        Assert.Equal("selenium_test_driver@example.com", email.GetAttribute("value"));
        Assert.Equal("0770000000", contactNumber.GetAttribute("value"));

        // DoD: "Email address is read-only". Confirm the input itself
        // is actually disabled/read-only in the DOM, not just
        // convention.
        Assert.NotNull(email.GetAttribute("readonly"));
    }

    // ============================================================
    // Scenario 2 - Successful profile update
    // ============================================================

    [Fact]
    public async Task UpdateProfile_ValidNameAndContact_ShowsSuccessAndReflectsImmediately()
    {
        var (username, password) = await TestDataSeeder.CreateFreshDriverAsync("upd");
        LoginAs(username, password);
        Driver.Navigate().GoToUrl($"{FrontendBaseUrl}/profile");

        var lastNameField = Wait.Until(d => d.FindElement(By.Id("lastName")));
        var newLastName = $"Updated{Guid.NewGuid():N}"[..12];

        // NOTE: WebDriver's native .Clear() sometimes doesn't fire the
        // events a React-controlled input listens for, leaving React's
        // internal state out of sync with the visible DOM value. Using
        // real keyboard input (select-all + delete) instead guarantees
        // proper onChange events fire, exactly like a real user typing.
        lastNameField.SendKeys(Keys.Control + "a");
        lastNameField.SendKeys(Keys.Delete);
        lastNameField.SendKeys(newLastName);

        Driver.FindElement(By.CssSelector("button[type='submit']")).Click();

        var successBanner = Wait.Until(d => d.FindElement(By.CssSelector("[role='status']")));
        Assert.Contains("Profile updated successfully.", successBanner.Text);

        // Reflected immediately on the page without a reload.
        var lastNameAfterSave = Driver.FindElement(By.Id("lastName"));
        Assert.Equal(newLastName, lastNameAfterSave.GetAttribute("value"));

        // Reflected after a real reload too (proves persistence, not
        // just optimistic local state).
        Driver.Navigate().Refresh();
        var lastNameAfterReload = Wait.Until(d => d.FindElement(By.Id("lastName")));
        Assert.Equal(newLastName, lastNameAfterReload.GetAttribute("value"));
    }

    // ============================================================
    // Scenario 3 - Invalid update
    // ============================================================

    [Fact]
    public async Task UpdateProfile_EmptyFirstName_ShowsInlineErrorAndDoesNotSave()
    {
        var (username, password) = await TestDataSeeder.CreateFreshDriverAsync("inv");
        LoginAs(username, password);
        Driver.Navigate().GoToUrl($"{FrontendBaseUrl}/profile");

        var firstNameField = Wait.Until(d => d.FindElement(By.Id("firstName")));
        var originalValue = firstNameField.GetAttribute("value");

        firstNameField.SendKeys(Keys.Control + "a");
        firstNameField.SendKeys(Keys.Delete);

        Driver.FindElement(By.CssSelector("button[type='submit']")).Click();

        // Client-side validation should catch this before any request
        // is even sent — the inline error appears next to the field.
        var inlineError = Wait.Until(d => d.FindElement(By.Id("firstName-error")));
        Assert.Contains("First name is required.", inlineError.Text);

        // No success banner should ever appear.
        var successBanners = Driver.FindElements(By.CssSelector("[role='status']"));
        Assert.Empty(successBanners);

        // Reload and confirm nothing was saved (the field is back to
        // whatever was actually persisted, not left blank).
        Driver.Navigate().Refresh();
        var firstNameAfterReload = Wait.Until(d => d.FindElement(By.Id("firstName")));
        Assert.Equal(originalValue, firstNameAfterReload.GetAttribute("value"));
        Assert.NotEqual(string.Empty, firstNameAfterReload.GetAttribute("value"));
    }

    // ============================================================
    // Scenario 4 - Password change
    // ============================================================

    [Fact]
    public async Task ChangePassword_ValidNewPassword_SucceedsAndStaysLoggedIn()
    {
        // Uses its own fresh account — password changes must never
        // touch the shared fixture account other tests depend on.
        var (username, originalPassword) = await TestDataSeeder.CreateFreshDriverAsync("pwd");
        LoginAs(username, originalPassword);
        Driver.Navigate().GoToUrl($"{FrontendBaseUrl}/profile");

        var newPassword = "ChangedPass123";

        var newPasswordField = Wait.Until(d => d.FindElement(By.Id("newPassword")));
        var confirmPasswordField = Driver.FindElement(By.Id("confirmPassword"));

        // Defensive: Chrome's password manager can autofill/suggest
        // content into a fresh type="password" field, and any leftover
        // suggestion dropdown can intercept the next click. Clear via
        // keyboard first, then press Escape to dismiss any popup.
        newPasswordField.SendKeys(Keys.Control + "a");
        newPasswordField.SendKeys(Keys.Delete);
        newPasswordField.SendKeys(newPassword);
        newPasswordField.SendKeys(Keys.Escape);

        confirmPasswordField.SendKeys(Keys.Control + "a");
        confirmPasswordField.SendKeys(Keys.Delete);
        confirmPasswordField.SendKeys(newPassword);
        confirmPasswordField.SendKeys(Keys.Escape);

        Driver.FindElement(By.CssSelector("button[type='submit']")).Click();

        var successBanner = Wait.Until(d => d.FindElement(By.CssSelector("[role='status']")));
        Assert.Contains("Profile updated successfully.", successBanner.Text);

        // AC: "I remain logged in with my current session (no forced
        // re-login required)." Prove we're still on an authenticated
        // page, not bounced back to /login.
        Assert.Contains("/profile", Driver.Url);
        Assert.DoesNotContain("/login", Driver.Url);

        // Confirm the new password actually works by logging out and
        // back in with it (best-effort: click the visible Logout
        // button, then log in fresh).
        Driver.FindElement(By.CssSelector(".topbar-logout")).Click();
        Wait.Until(d => d.Url.Contains("/login"));

        LoginAs(username, newPassword);
        Assert.DoesNotContain("/login", Driver.Url);
    }

    [Fact]
    public void ChangePassword_MismatchedConfirmation_ShowsInlineErrorAndDoesNotSubmit()
    {
        LoginAs(_seeded.Username, _seeded.Password);
        Driver.Navigate().GoToUrl($"{FrontendBaseUrl}/profile");

        Wait.Until(d => d.FindElement(By.Id("newPassword"))).SendKeys("SomeNewPass123");
        Driver.FindElement(By.Id("confirmPassword")).SendKeys("ADifferentPass456");

        Driver.FindElement(By.CssSelector("button[type='submit']")).Click();

        var inlineError = Wait.Until(d => d.FindElement(By.Id("confirmPassword-error")));
        Assert.Contains("Passwords do not match.", inlineError.Text);

        var successBanners = Driver.FindElements(By.CssSelector("[role='status']"));
        Assert.Empty(successBanners);
    }
}
