using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using AuthUserService.Data;
using AuthUserService.DTOs;
using AuthUserService.Services;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace AuthUserService.Tests;

public class DriverDeactivationDatabaseTests(CustomWebApplicationFactory factory) : IClassFixture<CustomWebApplicationFactory>
{
    [Fact]
    public async Task DeactivationPersistsInactiveFlagAndPreservesAccountAndPassword()
    {
        // Use the real Auth controller/repository and database, with controlled Trip
        // preparation. Real Trip SQL coordination is verified in its own DB tests.
        using var isolated = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            services.AddHttpClient<DriverDeactivationService>(client => client.BaseAddress = new Uri("http://trip-test/"))
                .ConfigurePrimaryHttpMessageHandler(() => new PreparedTripHandler())));
        using var admin = isolated.CreateClient();
        admin.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TestJwt.CreateToken("Admin"));
        var suffix = Guid.NewGuid().ToString("N")[..10];
        var registration = new CreateUserRequest
        {
            Username = "deact_" + suffix, Email = "deact_" + suffix + "@example.com",
            FirstName = "Deactivation", LastName = "Test", ContactNumber = "0770000000",
            Password = "ValidTestPass123!"
        };
        var created = await admin.PostAsJsonAsync("/api/users/drivers", registration);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var account = (await created.Content.ReadFromJsonAsync<UserResponse>())!;
        using var driver = isolated.CreateClient();
        var loggedIn = await driver.PostAsJsonAsync("/api/auth/login", new LoginRequest
        {
            Username = registration.Username, Password = registration.Password
        });
        Assert.Equal(HttpStatusCode.OK, loggedIn.StatusCode);
        var login = (await loggedIn.Content.ReadFromJsonAsync<LoginResponse>())!;
        driver.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login.Token);
        Assert.Equal(HttpStatusCode.OK, (await driver.GetAsync("/api/users/me")).StatusCode);
        using var scope = isolated.Services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<UserRepository>();
        var before = (await repository.GetByIdAsync(account.Id))!;

        Assert.Equal(HttpStatusCode.NoContent, (await admin.PostAsync($"/api/users/drivers/{account.Id}/deactivate", null)).StatusCode);
        var after = (await repository.GetByIdAsync(account.Id))!;
        Assert.False(after.IsActive);
        Assert.Equal(before.Id, after.Id);
        Assert.Equal(before.Username, after.Username);
        Assert.Equal(before.PasswordHash, after.PasswordHash);
        Assert.Equal(before.CreatedAt, after.CreatedAt);
        Assert.Equal(HttpStatusCode.Unauthorized, (await driver.GetAsync("/api/users/me")).StatusCode);
        driver.DefaultRequestHeaders.Authorization = null;
        Assert.Equal(HttpStatusCode.Unauthorized, (await driver.PostAsJsonAsync("/api/auth/login", new LoginRequest
        {
            Username = registration.Username, Password = registration.Password
        })).StatusCode);
        var drivers = await admin.GetFromJsonAsync<List<UserResponse>>("/api/users/drivers");
        Assert.Contains(drivers!, row => row.Id == account.Id && !row.IsActive);
        Assert.Equal(HttpStatusCode.NoContent, (await admin.PostAsync($"/api/users/drivers/{account.Id}/deactivate", null)).StatusCode);
    }

    private sealed class PreparedTripHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            Assert.Equal(HttpMethod.Post, request.Method);
            Assert.Equal("Bearer", request.Headers.Authorization?.Scheme);
            Assert.EndsWith("/deactivation-check", request.RequestUri!.AbsolutePath);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NoContent));
        }
    }
}
