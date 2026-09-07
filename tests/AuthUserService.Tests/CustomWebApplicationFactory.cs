using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace AuthUserService.Tests;

/// <summary>
/// Boots the real AuthUserService app in-memory for integration testing.
///
/// REQUIRES a reachable MySQL test database. Do NOT point this at the
/// same database you use for manual/demo testing — tests will insert
/// and read real rows.
///
/// Recommended setup: create a separate schema, e.g. by running
/// database/auth-user/schema.sql with the database name changed to
/// "fleet_identity_db_test", then update TestConnectionString below
/// (or your own MySQL credentials) to match.
///
/// IMPORTANT: config is applied via environment variables set in the
/// constructor, NOT via ConfigureAppConfiguration. AuthUserService's
/// Program.cs reads Jwt:Key etc. and throws immediately if missing —
/// this happens before builder.Build() runs, which is earlier than
/// WebApplicationFactory's ConfigureAppConfiguration hook fires.
/// Environment variables are read as soon as WebApplication.CreateBuilder(args)
/// runs (the very first line of Program.cs), so they arrive in time.
/// The "__" (double underscore) is ASP.NET Core's convention for nested
/// config keys via env vars — Jwt__Key becomes Jwt:Key.
/// </summary>
public class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    public const string JwtTestKey =
        "test-only-signing-key-not-for-production-use-0123456789";

    public const string JwtTestIssuer = "SmartFleet.AuthUserService.Tests";
    public const string JwtTestAudience = "SmartFleet.Frontend.Tests";

    // Update this connection string to match your local test database.
    public const string TestConnectionString =
        "Server=127.0.0.1;Port=3306;Database=fleet_identity_db_test;Uid=root;Pwd=test_root_password;";

    public static string EffectiveConnectionString =>
        Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
        ?? TestConnectionString;

    public CustomWebApplicationFactory()
    {
        Environment.SetEnvironmentVariable("Jwt__Key", JwtTestKey);
        Environment.SetEnvironmentVariable("Jwt__Issuer", JwtTestIssuer);
        Environment.SetEnvironmentVariable("Jwt__Audience", JwtTestAudience);
        Environment.SetEnvironmentVariable("Jwt__ExpirationMinutes", "60");
        Environment.SetEnvironmentVariable(
            "ConnectionStrings__DefaultConnection",
            EffectiveConnectionString);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
    }
}
