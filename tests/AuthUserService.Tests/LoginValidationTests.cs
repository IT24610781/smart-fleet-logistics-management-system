using AuthUserService.Data;
using AuthUserService.DTOs;
using AuthUserService.Services;
using Microsoft.Extensions.Configuration;

namespace AuthUserService.Tests;

/// <summary>
/// US-02: User Login with JWT
///
/// Pure unit tests for AuthService.LoginAsync's field validation.
/// These checks run before any database call, so no real MySQL
/// connection is needed here (the connection string below is never
/// actually opened).
/// </summary>
public class LoginValidationTests
{
    private static AuthService BuildAuthService()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] =
                    "Server=localhost;Database=unused_for_validation_tests;Uid=root;Pwd=unused;",
                ["Jwt:Key"] = "unused-key-not-reached-by-these-tests",
                ["Jwt:Issuer"] = "unused",
                ["Jwt:Audience"] = "unused",
                ["Jwt:ExpirationMinutes"] = "60"
            })
            .Build();

        var dbConnection = new DatabaseConnection(config);
        var repository = new UserRepository(dbConnection);
        var jwtService = new JwtService(config);
        return new AuthService(repository, jwtService);
    }

    [Fact]
    public async Task LoginAsync_MissingUsername_ThrowsArgumentException()
    {
        var service = BuildAuthService();
        var request = new LoginRequest { Username = " ", Password = "whatever" };

        var ex = await Assert.ThrowsAsync<ArgumentException>(
            () => service.LoginAsync(request));

        Assert.Equal("Username is required.", ex.Message);
    }

    [Fact]
    public async Task LoginAsync_MissingPassword_ThrowsArgumentException()
    {
        var service = BuildAuthService();
        var request = new LoginRequest { Username = "someuser", Password = "" };

        var ex = await Assert.ThrowsAsync<ArgumentException>(
            () => service.LoginAsync(request));

        Assert.Equal("Password is required.", ex.Message);
    }
}
