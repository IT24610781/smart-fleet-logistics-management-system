using AuthUserService.Data;
using AuthUserService.DTOs;
using AuthUserService.Services;
using Microsoft.Extensions.Configuration;

namespace AuthUserService.Tests;

/// <summary>
/// US-04: View and Edit Profile — Scenario 3 (Invalid update)
///
/// Pure unit tests for UserService.UpdateProfileAsync's field
/// validation. These checks run before any database call, so no real
/// MySQL connection is needed here.
/// </summary>
public class UpdateProfileValidationTests
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

    private static UpdateProfileRequest ValidRequest() => new()
    {
        FirstName = "Jane",
        LastName = "Doe",
        ContactNumber = "0771234567"
    };

    [Fact]
    public async Task UpdateProfileAsync_MissingFirstName_ThrowsArgumentException()
    {
        var service = BuildUserService();
        var request = ValidRequest();
        request.FirstName = " ";

        var ex = await Assert.ThrowsAsync<ArgumentException>(
            () => service.UpdateProfileAsync(Guid.NewGuid(), request));

        Assert.Equal("First name is required.", ex.Message);
    }

    [Fact]
    public async Task UpdateProfileAsync_MissingLastName_ThrowsArgumentException()
    {
        var service = BuildUserService();
        var request = ValidRequest();
        request.LastName = "";

        var ex = await Assert.ThrowsAsync<ArgumentException>(
            () => service.UpdateProfileAsync(Guid.NewGuid(), request));

        Assert.Equal("Last name is required.", ex.Message);
    }

    [Fact]
    public async Task UpdateProfileAsync_MissingContactNumber_ThrowsArgumentException()
    {
        var service = BuildUserService();
        var request = ValidRequest();
        request.ContactNumber = "";

        var ex = await Assert.ThrowsAsync<ArgumentException>(
            () => service.UpdateProfileAsync(Guid.NewGuid(), request));

        Assert.Equal("Contact number is required.", ex.Message);
    }

    [Fact]
    public async Task UpdateProfileAsync_NewPasswordShorterThan8Chars_ThrowsArgumentException()
    {
        var service = BuildUserService();
        var request = ValidRequest();
        request.Password = "short1"; // 6 chars

        var ex = await Assert.ThrowsAsync<ArgumentException>(
            () => service.UpdateProfileAsync(Guid.NewGuid(), request));

        Assert.Equal("Password must be at least 8 characters.", ex.Message);
    }

    [Fact]
    public async Task UpdateProfileAsync_PasswordOmitted_DoesNotThrowPasswordError()
    {
        // Password is optional on profile update ("leave blank to keep
        // current password"). Omitting it should never trigger the
        // length check. This will go on to attempt a real DB call
        // (GetByIdAsync), which throws a connection-related exception
        // here since the connection string is fake — that's expected;
        // it just proves we got past validation without an
        // ArgumentException about the password.
        var service = BuildUserService();
        var request = ValidRequest(); // Password left null

        var ex = await Record.ExceptionAsync(
            () => service.UpdateProfileAsync(Guid.NewGuid(), request));

        Assert.IsNotType<ArgumentException>(ex);
    }

    [Fact]
    public async Task UpdateProfileAsync_ContactNumberIsClearlyInvalidButNonEmpty_DoesNotThrow()
    {
        // ⚠ KNOWN GAP vs the frontend's own validation rules.
        //
        // The frontend (utils/validation.js -> isValidContactNumber)
        // requires 7-15 digits. The backend's UpdateProfileAsync only
        // checks IsNullOrWhiteSpace — it has NO format validation at
        // all. This test documents that a clearly-invalid value like
        // "abc" is currently accepted by the backend as long as it's
        // non-empty, which is a gap between the two layers (and a
        // silent trust in the frontend never being bypassed, e.g. via
        // a direct API call or Postman).
        var service = BuildUserService();
        var request = ValidRequest();
        request.ContactNumber = "abc"; // passes frontend's own regex? No — but backend doesn't check format at all.

        var ex = await Record.ExceptionAsync(
            () => service.UpdateProfileAsync(Guid.NewGuid(), request));

        // Not an ArgumentException — proves the backend accepted the
        // format and moved on to the (here, failing) DB call instead.
        Assert.IsNotType<ArgumentException>(ex);
    }
}
