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
///
/// HOW THE PAGE WORKS (redesigned):
///   - VIEW mode shows the details as plain text: <dd id="view-firstName">,
///     view-lastName, view-username, view-email, view-contactNumber, view-role.
///   - Clicking "Edit details" (id="edit-profile-button") switches to EDIT
///     mode: inputs #firstName, #lastName, #contactNumber are editable;
///     #username, #email, #role are read-only. "Save changes" is the
///     submit button; "Cancel" is id="cancel-edit-button" and asks for
///     confirmation ("Discard changes") when something was changed.
///   - There are NO password fields on this page. A logged-in session must
///     not be able to change the password without re-verifying the current
///     one, so the old ChangePassword_* tests were removed with the feature.
/// </summary>
[Collection("Selenium collection")]
public class ProfilePageTests : SeleniumTestBase
{
    private readonly SeededDriverFixture _seeded;

    public ProfilePageTests(SeededDriverFixture seeded)
    {
        _seeded = seeded;
    }

    // ---------- helpers ----------

    private void OpenProfile()
    {
        Driver.Navigate().GoToUrl($"{FrontendBaseUrl}/profile");
    }

    // Text shown in VIEW mode for a field (firstName, lastName, email, ...).
    private string ViewText(string field)
    {
        return Wait.Until(d => d.FindElement(By.Id($"view-{field}"))).Text;
    }

    private void ClickEditDetails()
    {
        Wait.Until(d => d.FindElement(By.Id("edit-profile-button"))).Click();
        Wait.Until(d => d.FindElement(By.Id("firstName")));
    }

    // NOTE: WebDriver's native .Clear() sometimes doesn't fire the
    // events a React-controlled input listens for, leaving React's
    // internal state out of sync with the visible DOM value. Using
    // real keyboard input (select-all + delete) instead guarantees
    // proper onChange events fire, exactly like a real user typing.
    private static void ClearField(IWebElement field)
    {
        field.SendKeys(Keys.Control + "a");
        field.SendKeys(Keys.Delete);
    }

    // ============================================================
    // Scenario 1 - View profile
    // ============================================================

    [Fact]
    public void ViewProfile_ShowsCurrentDetailsAsReadOnlyText()
    {
        LoginAs(_seeded.Username, _seeded.Password);

        // Navigate via the real UI element (the user chip in the top bar),
        // not a direct URL — this is literally what the AC describes
        // ("navigate to My Profile").
        Driver.FindElement(By.CssSelector("header a[href='/profile']")).Click();

        Wait.Until(d => d.Url.Contains("/profile"));

        Assert.Equal("Selenium", ViewText("firstName"));
        Assert.Equal("Tester", ViewText("lastName"));
        Assert.Equal("selenium_test_driver@example.com", ViewText("email"));
        Assert.Equal("0770000000", ViewText("contactNumber"));

        // View mode is plain text: no editable inputs until "Edit details".
        Assert.Empty(Driver.FindElements(By.Id("firstName")));
        Assert.True(Driver.FindElement(By.Id("edit-profile-button")).Displayed);
    }

    [Fact]
    public void EditDetails_EmailUsernameAndRoleAreReadOnly_NameAndContactAreEditable()
    {
        LoginAs(_seeded.Username, _seeded.Password);
        OpenProfile();
        ClickEditDetails();

        // DoD: "Email address is read-only". Confirm the input itself
        // is actually read-only in the DOM, not just convention.
        Assert.NotNull(Driver.FindElement(By.Id("email")).GetAttribute("readonly"));
        Assert.NotNull(Driver.FindElement(By.Id("username")).GetAttribute("readonly"));
        Assert.NotNull(Driver.FindElement(By.Id("role")).GetAttribute("readonly"));

        Assert.Null(Driver.FindElement(By.Id("firstName")).GetAttribute("readonly"));
        Assert.Null(Driver.FindElement(By.Id("lastName")).GetAttribute("readonly"));
        Assert.Null(Driver.FindElement(By.Id("contactNumber")).GetAttribute("readonly"));

        // Inputs are pre-filled with the current values.
        Assert.Equal("Selenium", Driver.FindElement(By.Id("firstName")).GetAttribute("value"));
        Assert.Equal("selenium_test_driver@example.com", Driver.FindElement(By.Id("email")).GetAttribute("value"));
    }

    // ============================================================
    // Scenario 2 - Successful profile update
    // ============================================================

    [Fact]
    public async Task UpdateProfile_ValidNameAndContact_ShowsSuccessAndReflectsImmediately()
    {
        var (username, password) = await TestDataSeeder.CreateFreshDriverAsync("upd");
        LoginAs(username, password);
        OpenProfile();
        ClickEditDetails();

        var lastNameField = Driver.FindElement(By.Id("lastName"));
        var newLastName = $"Updated{Guid.NewGuid():N}"[..12];

        ClearField(lastNameField);
        lastNameField.SendKeys(newLastName);

        Driver.FindElement(By.CssSelector("button[type='submit']")).Click();

        var successBanner = Wait.Until(d => d.FindElement(By.CssSelector("[role='status']")));
        Assert.Contains("Profile updated successfully.", successBanner.Text);

        // Back in view mode, reflected immediately without a reload.
        Assert.Equal(newLastName, ViewText("lastName"));

        // Reflected after a real reload too (proves persistence, not
        // just optimistic local state).
        Driver.Navigate().Refresh();
        Assert.Equal(newLastName, ViewText("lastName"));
    }

    // ============================================================
    // Scenario 3 - Invalid update
    // ============================================================

    [Fact]
    public async Task UpdateProfile_EmptyFirstName_ShowsInlineErrorAndDoesNotSave()
    {
        var (username, password) = await TestDataSeeder.CreateFreshDriverAsync("inv");
        LoginAs(username, password);
        OpenProfile();

        var originalValue = ViewText("firstName");
        Assert.NotEqual(string.Empty, originalValue);

        ClickEditDetails();
        ClearField(Driver.FindElement(By.Id("firstName")));

        Driver.FindElement(By.CssSelector("button[type='submit']")).Click();

        // Client-side validation should catch this before any request
        // is even sent — the inline error appears next to the field.
        var inlineError = Wait.Until(d => d.FindElement(By.Id("firstName-error")));
        Assert.Contains("First name is required.", inlineError.Text);

        // No success banner should ever appear.
        Assert.Empty(Driver.FindElements(By.CssSelector("[role='status']")));

        // Reload and confirm nothing was saved (back to whatever was
        // actually persisted, not left blank).
        Driver.Navigate().Refresh();
        Assert.Equal(originalValue, ViewText("firstName"));
    }

    // ============================================================
    // Scenario 4 - Cancel discards unsaved changes
    // ============================================================

    [Fact]
    public async Task EditDetails_Cancel_AsksToConfirmAndDiscardsChanges()
    {
        var (username, password) = await TestDataSeeder.CreateFreshDriverAsync("cnl");
        LoginAs(username, password);
        OpenProfile();

        var originalLastName = ViewText("lastName");

        ClickEditDetails();
        Driver.FindElement(By.Id("lastName")).SendKeys("Changed");

        Driver.FindElement(By.Id("cancel-edit-button")).Click();

        // Something was changed, so the user is asked before losing it.
        var dialog = Wait.Until(d => d.FindElement(By.CssSelector("[role='dialog']")));
        Assert.Contains("Discard", dialog.Text);

        Driver.FindElement(By.XPath("//div[@role='dialog']//button[normalize-space()='Discard changes']")).Click();

        // Back to view mode with the ORIGINAL value, and nothing saved.
        Assert.Equal(originalLastName, ViewText("lastName"));
        Assert.Empty(Driver.FindElements(By.CssSelector("[role='status']")));

        Driver.Navigate().Refresh();
        Assert.Equal(originalLastName, ViewText("lastName"));
    }

    // ============================================================
    // Security - no password fields on the profile page
    // ============================================================

    [Fact]
    public void ProfilePage_HasNoPasswordFields_InViewOrEditMode()
    {
        LoginAs(_seeded.Username, _seeded.Password);
        OpenProfile();

        Wait.Until(d => d.FindElement(By.Id("edit-profile-button")));
        Assert.Empty(Driver.FindElements(By.CssSelector("input[type='password']")));

        ClickEditDetails();
        Assert.Empty(Driver.FindElements(By.CssSelector("input[type='password']")));
        Assert.Empty(Driver.FindElements(By.Id("newPassword")));
        Assert.Empty(Driver.FindElements(By.Id("confirmPassword")));
    }
}
