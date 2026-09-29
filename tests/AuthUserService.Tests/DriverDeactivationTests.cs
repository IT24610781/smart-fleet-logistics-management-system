using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using AuthUserService.Controllers;
using AuthUserService.Data;
using AuthUserService.Models;
using AuthUserService.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Microsoft.Extensions.Hosting;

namespace AuthUserService.Tests;

public class DriverDeactivationTests
{
    private static readonly Guid DriverId = Guid.NewGuid();
    private const string Key = "driver-deactivation-test-key-at-least-32-bytes-long";
    private static IConfiguration Config() => new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
    {
        ["Jwt:Key"] = Key, ["Jwt:Issuer"] = "TestAuth", ["Jwt:Audience"] = "TestFleet",
        ["Jwt:ExpirationMinutes"] = "20", ["ConnectionStrings:DefaultConnection"] = "Server=unused;Database=unused;"
    }).Build();

    [Fact]
    public async Task ActiveTripConflictLeavesAccountActive()
    {
        var users = new Accounts();
        using var client = Client("{}", HttpStatusCode.Conflict);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new DriverDeactivationService(users, client).DeactivateAsync(DriverId, "Bearer admin", default));
        Assert.True(users.Account!.IsActive);
        Assert.Equal(0, users.Writes);
    }

    [Fact]
    public async Task TripPreparationAllowsDeactivation()
    {
        var users = new Accounts();
        using var client = Client("", HttpStatusCode.NoContent);
        await new DriverDeactivationService(users, client).DeactivateAsync(DriverId, "Bearer admin", default);
        Assert.False(users.Account!.IsActive);
        Assert.Equal(1, users.Writes);
    }

    [Theory]
    [InlineData("null", 200)]
    [InlineData("invalid json", 200)]
    [InlineData("[{}]", 200)]
    [InlineData("[]", 503)]
    [InlineData("[]", 401)]
    public async Task DependencyErrorsLeaveDriverActive(string body, int status)
    {
        var users = new Accounts();
        using var client = Client(body, (HttpStatusCode)status);
        await Assert.ThrowsAsync<DriverDependencyException>(() =>
            new DriverDeactivationService(users, client).DeactivateAsync(DriverId, "Bearer admin", default));
        Assert.True(users.Account!.IsActive);
        Assert.Equal(0, users.Writes);
    }

    [Fact]
    public async Task TimeoutLeavesDriverActive()
    {
        var users = new Accounts();
        using var client = new HttpClient(new ResponseHandler(_ => throw new TaskCanceledException())) { BaseAddress = new Uri("http://trip/") };
        await Assert.ThrowsAsync<DriverDependencyException>(() =>
            new DriverDeactivationService(users, client).DeactivateAsync(DriverId, "Bearer admin", default));
        Assert.True(users.Account!.IsActive);
    }

    [Fact]
    public async Task AlreadyInactiveIsIdempotentAndDoesNotCallTripService()
    {
        var users = new Accounts(); users.Account!.IsActive = false;
        using var client = new HttpClient(new ResponseHandler(_ => throw new Exception("Must not call Trip")));
        await new DriverDeactivationService(users, client).DeactivateAsync(DriverId, "Bearer admin", default);
        Assert.Equal(0, users.Writes);
    }

    [Theory]
    [InlineData("Admin")]
    [InlineData("Dispatcher")]
    [InlineData("missing")]
    public async Task CannotDeactivateOtherAccountTypes(string role)
    {
        var users = new Accounts();
        if (role == "missing") users.Account = null; else users.Account!.Role = role;
        using var client = Client("[]");
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            new DriverDeactivationService(users, client).DeactivateAsync(DriverId, "Bearer admin", default));
        Assert.Equal(0, users.Writes);
    }

    [Fact]
    public async Task ExistingTokenAndLoginAreRejectedAfterAdminDeactivation()
    {
        var users = new Accounts();
        using var server = Server(users);
        using var client = server.GetTestClient();
        var jwt = new JwtService(Config());
        var driverToken = jwt.GenerateToken(users.Account!).Token;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", driverToken);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/driver-probe")).StatusCode);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer",
            jwt.GenerateToken(new User { Id = Guid.NewGuid(), Role = "Admin", Username = "admin" }).Token);
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsync($"/api/users/drivers/{DriverId}/deactivate", null)).StatusCode);
        Assert.False(users.Account!.IsActive);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", driverToken);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/driver-probe")).StatusCode);
        var login = await client.PostAsJsonAsync("/api/auth/login", new { username = "driver", password = "whatever" });
        Assert.Equal(HttpStatusCode.Unauthorized, login.StatusCode);
        Assert.Contains("inactive", await login.Content.ReadAsStringAsync());
    }

    [Theory]
    [InlineData(null, 401)]
    [InlineData("Driver", 403)]
    [InlineData("Dispatcher", 403)]
    public async Task OnlyAdminCanDeactivateThroughApi(string? role, int expected)
    {
        var users = new Accounts();
        using var server = Server(users);
        using var client = server.GetTestClient();
        if (role != null)
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer",
                new JwtService(Config()).GenerateToken(new User { Id = DriverId, Role = role, Username = "caller" }).Token);
        Assert.Equal((HttpStatusCode)expected, (await client.PostAsync($"/api/users/drivers/{DriverId}/deactivate", null)).StatusCode);
        Assert.True(users.Account!.IsActive);
    }

    [Theory]
    [InlineData(409)]
    [InlineData(503)]
    public async Task ApiReportsBlockedOrUnverifiableTrips(int status)
    {
        var users = new Accounts();
        using var server = Server(users, (HttpStatusCode)status);
        using var client = server.GetTestClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer",
            new JwtService(Config()).GenerateToken(new User { Id = Guid.NewGuid(), Role = "Admin", Username = "admin" }).Token);
        Assert.Equal((HttpStatusCode)status, (await client.PostAsync($"/api/users/drivers/{DriverId}/deactivate", null)).StatusCode);
        Assert.True(users.Account!.IsActive);
        Assert.Equal(0, users.Writes);
    }

    private static IHost Server(Accounts users, HttpStatusCode tripStatus = HttpStatusCode.NoContent) => new HostBuilder().ConfigureWebHost(web => web.UseTestServer()
        .ConfigureServices(services =>
        {
            services.AddSingleton(Config());
            services.AddLogging();
            services.AddSingleton<IUserAccountRepository>(users);
            services.AddSingleton(new UserService(new UserRepository(new DatabaseConnection(Config()))));
            services.AddSingleton<JwtService>();
            services.AddSingleton<AuthService>();
            services.AddSingleton(new DriverDeactivationService(users, Client("", tripStatus)));
            services.AddControllers().AddApplicationPart(typeof(UsersController).Assembly);
            services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidIssuer = "TestAuth", ValidAudience = "TestFleet",
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Key)),
                    ValidateIssuer = true, ValidateAudience = true, ValidateIssuerSigningKey = true, ValidateLifetime = true
                };
                options.Events = DriverTokenEvents.Create();
            });
            services.AddAuthorization();
        })
        .Configure(app =>
        {
            app.UseRouting(); app.UseAuthentication(); app.UseAuthorization();
            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
                endpoints.MapGet("/driver-probe", () => "allowed").RequireAuthorization(policy => policy.RequireRole("Driver"));
            });
        })).Start();

    private static HttpClient Client(string body, HttpStatusCode status = HttpStatusCode.OK) =>
        new(new ResponseHandler(request =>
        {
            Assert.Equal("Bearer", request.Headers.Authorization?.Scheme);
            Assert.Equal(HttpMethod.Post, request.Method);
            Assert.Equal($"/api/trips/drivers/{DriverId}/deactivation-check", request.RequestUri!.AbsolutePath);
            return new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
        })) { BaseAddress = new Uri("http://trip/") };

    private sealed class ResponseHandler(Func<HttpRequestMessage, HttpResponseMessage> response) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token) => Task.FromResult(response(request));
    }

    private sealed class Accounts : IUserAccountRepository
    {
        public User? Account = new() { Id = DriverId, Username = "driver", Role = "Driver", IsActive = true };
        public int Writes;
        public Task<User?> GetByIdAsync(Guid id) => Task.FromResult(Account?.Id == id ? Account : null);
        public Task<User?> GetByUsernameAsync(string username) => Task.FromResult(Account);
        public Task DeactivateDriverAsync(Guid id) { Writes++; Account!.IsActive = false; return Task.CompletedTask; }
    }
}
