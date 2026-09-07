using AuthUserService.Data;
using AuthUserService.DTOs;
using AuthUserService.Services;
using Microsoft.Extensions.Configuration;

namespace AuthUserService.Tests;

/// <summary>
/// US-01: Admin Creates User Account — Scenario 3 (Invalid or missing fields)
///
/// These are pure unit tests: UserService.CreateUserAsync validates all
/// required fields and password length BEFORE it ever touches the
/// repository/database, so no real MySQL connection is needed here.
/// The connection string below is never actually opened.
/// </summary>
public class CreateUserValidationTests
{
    private static UserService BuildUserService()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] =
                    "Server=localhost;Database=unused_for_validation_tests;Uid=root;Pwd=unused;"
            })
            .Build();

        var dbConnection = new DatabaseConnection(config);
        var repository = new UserRepository(dbConnection);
        return new UserService(repository);
    }

    private static CreateUserRequest ValidRequest() => new()
    {
        Username = "jdoe",
        Email = "jdoe@example.com",
        Password = "Passw0rd!",
        FirstName = "John",
        LastName = "Doe",
        ContactNumber = "0771234567"
    };

    [Fact]
    public async Task CreateUserAsync_MissingUsername_ThrowsArgumentException()
    {
        var service = BuildUserService();
        var request = ValidRequest();
        request.Username = " ";

        var ex = await Assert.ThrowsAsync<ArgumentException>(
            () => service.CreateUserAsync(request));

        Assert.Equal("Username is required.", ex.Message);
    }

    [Fact]
    public async Task CreateUserAsync_MissingEmail_ThrowsArgumentException()
    {
        var service = BuildUserService();
        var request = ValidRequest();
        request.Email = "";

        var ex = await Assert.ThrowsAsync<ArgumentException>(
            () => service.CreateUserAsync(request));

        Assert.Equal("Email is required.", ex.Message);
    }

    [Fact]
    public async Task CreateUserAsync_MissingPassword_ThrowsArgumentException()
    {
        var service = BuildUserService();
        var request = ValidRequest();
        request.Password = "";

        var ex = await Assert.ThrowsAsync<ArgumentException>(
            () => service.CreateUserAsync(request));

        Assert.Equal("Password is required.", ex.Message);
    }

    [Fact]
    public async Task CreateUserAsync_MissingFirstName_ThrowsArgumentException()
    {
        var service = BuildUserService();
        var request = ValidRequest();
        request.FirstName = "";

        var ex = await Assert.ThrowsAsync<ArgumentException>(
            () => service.CreateUserAsync(request));

        Assert.Equal("First name is required.", ex.Message);
    }

    [Fact]
    public async Task CreateUserAsync_MissingLastName_ThrowsArgumentException()
    {
        var service = BuildUserService();
        var request = ValidRequest();
        request.LastName = "";

        var ex = await Assert.ThrowsAsync<ArgumentException>(
            () => service.CreateUserAsync(request));

        Assert.Equal("Last name is required.", ex.Message);
    }

    [Fact]
    public async Task CreateUserAsync_MissingContactNumber_ThrowsArgumentException()
    {
        var service = BuildUserService();
        var request = ValidRequest();
        request.ContactNumber = "";

        var ex = await Assert.ThrowsAsync<ArgumentException>(
            () => service.CreateUserAsync(request));

        Assert.Equal("Contact number is required.", ex.Message);
    }

    [Fact]
    public async Task CreateUserAsync_PasswordExactly7Chars_ThrowsLengthError()
    {
        var service = BuildUserService();
        var request = ValidRequest();
        request.Password = "1234567"; // 7 chars — not empty, just too short

        var ex = await Assert.ThrowsAsync<ArgumentException>(
            () => service.CreateUserAsync(request));

        Assert.Equal("Password must be at least 8 characters.", ex.Message);
    }

    [Fact]
    public async Task CreateUserAsync_PasswordExactly8Chars_DoesNotThrowLengthError()
    {
        // Boundary check: 8 chars should pass length validation.
        // It will then attempt a real DB call (GetByUsernameAsync), which
        // will throw a connection-related exception here since the
        // connection string is fake — that's expected and fine, it just
        // proves we got past the length check without an ArgumentException.
        var service = BuildUserService();
        var request = ValidRequest();
        request.Password = "12345678"; // exactly 8 chars

        var ex = await Record.ExceptionAsync(() => service.CreateUserAsync(request));

        Assert.IsNotType<ArgumentException>(ex);
    }
}
