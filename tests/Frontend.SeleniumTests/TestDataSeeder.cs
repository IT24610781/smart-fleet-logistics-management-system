using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.IdentityModel.Tokens;

namespace Frontend.SeleniumTests;

/// <summary>
/// Bootstraps a known test user account against the REAL running
/// AuthUserService (not an in-memory test host — Selenium drives an
/// actual browser against actual localhost servers).
///
/// WHY THIS EXISTS: there is currently no seeded Admin account and no
/// self-registration path for Admin anywhere in the system. The only
/// way any user account gets created is via the Admin-only
/// POST /api/users/drivers endpoint — which itself requires an
/// existing Admin token. That's a real bootstrapping gap (worth
/// flagging to the team separately). To work around it for testing
/// purposes, this seeder mints its own short-lived Admin JWT by
/// reading the JWT signing key directly out of YOUR OWN local
/// services/AuthUserService/appsettings.Development.json at runtime.
/// The key is never hardcoded or committed anywhere — if that file
/// doesn't exist or doesn't have Jwt config, this will fail loudly
/// with a clear error instead of silently doing something insecure.
/// </summary>
public static class TestDataSeeder
{
    private const string BackendBaseUrl = "http://localhost:5100";

    // Adjust this if your repo layout differs from the standard one.
    private const string RelativePathToDevSettings =
        "../../../../../services/AuthUserService/appsettings.Development.json";

    public static async Task<(string Username, string Password)> EnsureTestDriverExistsAsync()
    {
        var (key, issuer, audience) = ReadJwtConfigFromLocalDevSettings();

        var adminToken = MintAdminToken(key, issuer, audience);

        using var client = new HttpClient { BaseAddress = new Uri(BackendBaseUrl) };
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", adminToken);

        const string username = "selenium_test_driver";
        const string password = "SeleniumPass123";

        var response = await client.PostAsJsonAsync("/api/users/drivers", new
        {
            username,
            email = "selenium_test_driver@example.com",
            password,
            firstName = "Selenium",
            lastName = "Tester",
            contactNumber = "0770000000"
        });

        // 201 = created fresh. 409 = already exists from a previous
        // run — both are fine, the account is usable either way.
        if (response.StatusCode != System.Net.HttpStatusCode.Created &&
            response.StatusCode != System.Net.HttpStatusCode.Conflict)
        {
            var body = await response.Content.ReadAsStringAsync();
            throw new InvalidOperationException(
                $"Failed to seed test driver account. Status: {response.StatusCode}. Body: {body}. " +
                "Is AuthUserService actually running at http://localhost:5000?");
        }

        return (username, password);
    }

    /// <summary>
    /// Creates a brand-new, uniquely-named driver account for a single
    /// test to own exclusively. Use this (instead of the shared
    /// SeededDriverFixture account) for any test that MUTATES account
    /// data — name, contact number, or password — so a change made by
    /// one test can never affect another test's login or assertions.
    /// </summary>
    public static async Task<(string Username, string Password)> CreateFreshDriverAsync(string suffix)
    {
        var (key, issuer, audience) = ReadJwtConfigFromLocalDevSettings();
        var adminToken = MintAdminToken(key, issuer, audience);

        using var client = new HttpClient { BaseAddress = new Uri(BackendBaseUrl) };
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", adminToken);

        // Keep this within the frontend's username rules (3-20 chars,
        // starts with a letter, letters/numbers/underscore/dot only).
        var username = $"sel{suffix}{Guid.NewGuid():N}"[..18];
        const string password = "SeleniumFreshPass123";

        var response = await client.PostAsJsonAsync("/api/users/drivers", new
        {
            username,
            email = $"{username}@example.com",
            password,
            firstName = "Selenium",
            lastName = "Tester",
            contactNumber = "0770000000"
        });

        if (response.StatusCode != System.Net.HttpStatusCode.Created)
        {
            var body = await response.Content.ReadAsStringAsync();
            throw new InvalidOperationException(
                $"Failed to create fresh test driver account. Status: {response.StatusCode}. Body: {body}.");
        }

        return (username, password);
    }

    private static (string Key, string Issuer, string Audience) ReadJwtConfigFromLocalDevSettings()
    {
        var path = Path.Combine(AppContext.BaseDirectory, RelativePathToDevSettings);
        path = Path.GetFullPath(path);

        if (!File.Exists(path))
        {
            throw new FileNotFoundException(
                $"Could not find appsettings.Development.json at expected path: {path}. " +
                "Update RelativePathToDevSettings in TestDataSeeder.cs if your folder layout differs.");
        }

        using var stream = File.OpenRead(path);
        using var doc = JsonDocument.Parse(stream);

        var jwt = doc.RootElement.GetProperty("Jwt");
        var key = jwt.GetProperty("Key").GetString()
            ?? throw new InvalidOperationException("Jwt:Key missing from appsettings.Development.json.");
        var issuer = jwt.GetProperty("Issuer").GetString()
            ?? throw new InvalidOperationException("Jwt:Issuer missing from appsettings.Development.json.");
        var audience = jwt.GetProperty("Audience").GetString()
            ?? throw new InvalidOperationException("Jwt:Audience missing from appsettings.Development.json.");

        return (key, issuer, audience);
    }

    private static string MintAdminToken(string key, string issuer, string audience)
    {
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, Guid.NewGuid().ToString()),
            new(JwtRegisteredClaimNames.UniqueName, "selenium_seed_admin"),
            new(JwtRegisteredClaimNames.Email, "selenium_seed_admin@example.com"),
            new(ClaimTypes.Role, "Admin")
        };

        var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key));
        var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(5),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
