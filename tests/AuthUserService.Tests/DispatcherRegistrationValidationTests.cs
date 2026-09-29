using AuthUserService.Data;
using AuthUserService.DTOs;
using AuthUserService.Services;
using Microsoft.Extensions.Configuration;

namespace AuthUserService.Tests;

/// <summary>US08 validation must reject invalid registration before accessing MySQL.</summary>
public class DispatcherRegistrationValidationTests
{
    [Theory]
    [InlineData("username", "")]
    [InlineData("username", "bad name!")]
    [InlineData("email", "bad")]
    [InlineData("password", "short")]
    [InlineData("firstName", "")]
    [InlineData("lastName", "")]
    [InlineData("contactNumber", "abc")]
    public async Task RegisterDispatcher_InvalidField_ReturnsFieldErrorBeforeDatabaseAccess(
        string field, string value)
    {
        // An unreachable database makes accidental persistence fail this test.
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] =
                    "Server=127.0.0.1;Port=1;Database=unused;Uid=unused;Pwd=unused;Connection Timeout=1;"
            })
            .Build();
        var service = new UserService(new UserRepository(new DatabaseConnection(configuration)));
        var request = new CreateUserRequest
        {
            Username = "qavalid",
            Email = "qavalid@example.com",
            Password = "QaValidation123!",
            FirstName = "QA",
            LastName = "Tester",
            ContactNumber = "0771234567"
        };

        switch (field)
        {
            case "username": request.Username = value; break;
            case "email": request.Email = value; break;
            case "password": request.Password = value; break;
            case "firstName": request.FirstName = value; break;
            case "lastName": request.LastName = value; break;
            case "contactNumber": request.ContactNumber = value; break;
        }

        var error = await Assert.ThrowsAsync<ValidationException>(
            () => service.RegisterDispatcherAsync(request));

        Assert.Contains(field, error.Errors.Keys);
    }
}
