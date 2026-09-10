using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Threading;
using Microsoft.IdentityModel.Tokens;
using OpenQA.Selenium;

namespace Frontend.SeleniumTests;

/// <summary>
/// Logs the browser in "as Admin" WITHOUT going through a real login
/// form, because there is currently no seeded Admin account and no
/// Admin self-registration path anywhere in the system (the only way
/// to create ANY account is via the Admin-only POST /api/users/drivers
/// endpoint, which itself requires an existing Admin — a genuine
/// bootstrapping gap worth raising with the team separately).
///
/// This mints a real, validly-signed JWT (using the same technique as
/// TestDataSeeder) and writes it directly into localStorage under the
/// exact keys AuthContext.jsx uses (smartfleet_token / smartfleet_user),
/// then reloads so the app picks up the "session" as if a real login
/// had happened. This is a test-only workaround, not a feature of the
/// app — Admin pages tested this way are exercising real app code
/// (routing, forms, API calls), just skipping the (currently
/// nonexistent) login step for a role nothing can actually log in as
/// yet.
/// </summary>
public static class AdminSessionInjector
{
    public static void LoginAsAdminViaLocalStorage(IWebDriver driver, string frontendBaseUrl)
    {
        var (key, issuer, audience) = ReadJwtConfigFromLocalDevSettings();

        var adminId = Guid.NewGuid();
        var token = MintAdminToken(key, issuer, audience, adminId);

        var user = new
        {
            id = adminId.ToString(),
            username = "selenium_admin_session",
            email = "selenium_admin_session@example.com",
            firstName = "Selenium",
            lastName = "Admin",
            contactNumber = "0770000000",
            role = "Admin"
        };
        var userJson = JsonSerializer.Serialize(user);

        // Must be on the app's own origin before localStorage calls apply.
        driver.Navigate().GoToUrl($"{frontendBaseUrl}/login");

        var js = (IJavaScriptExecutor)driver;
        js.ExecuteScript(
            "localStorage.setItem(arguments[0], arguments[1]); localStorage.setItem(arguments[2], arguments[3]);",
            "smartfleet_token", token, "smartfleet_user", userJson);

        driver.Navigate().Refresh();

        // The app's own routing likely redirects away from /login once
        // it sees a valid session on load. If we fire the next
        // navigation command while that client-side redirect is still
        // resolving, ChromeDriver can hang waiting for a stable page
        // state — this has been the actual cause of intermittent
        // WebDriverException timeouts in tests that call this method.
        // Wait here until the browser has genuinely left /login before
        // handing control back.
        var deadline = DateTime.UtcNow.AddSeconds(15);
        while (driver.Url.Contains("/login") && DateTime.UtcNow < deadline)
        {
            Thread.Sleep(200);
        }
    }

    private const string RelativePathToDevSettings =
        "../../../../../services/AuthUserService/appsettings.Development.json";

    private static (string Key, string Issuer, string Audience) ReadJwtConfigFromLocalDevSettings()
    {
        var path = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, RelativePathToDevSettings));

        if (!File.Exists(path))
        {
            throw new FileNotFoundException(
                $"Could not find appsettings.Development.json at expected path: {path}. " +
                "Update RelativePathToDevSettings if your folder layout differs.");
        }

        using var stream = File.OpenRead(path);
        using var doc = JsonDocument.Parse(stream);
        var jwt = doc.RootElement.GetProperty("Jwt");

        return (
            jwt.GetProperty("Key").GetString()!,
            jwt.GetProperty("Issuer").GetString()!,
            jwt.GetProperty("Audience").GetString()!);
    }

    private static string MintAdminToken(string key, string issuer, string audience, Guid subjectId)
    {
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, subjectId.ToString()),
            new(JwtRegisteredClaimNames.UniqueName, "selenium_admin_session"),
            new(JwtRegisteredClaimNames.Email, "selenium_admin_session@example.com"),
            new(ClaimTypes.Role, "Admin")
        };

        var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key));
        var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(15),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
