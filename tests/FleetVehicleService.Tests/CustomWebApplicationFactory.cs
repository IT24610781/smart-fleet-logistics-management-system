using FleetVehicleService.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace FleetVehicleService.Tests;

public class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    public const string JwtTestKey =
        "test-only-signing-key-not-for-production-use-0123456789";

    public const string JwtTestIssuer = "SmartFleet.FleetVehicleService.Tests";
    public const string JwtTestAudience = "SmartFleet.Frontend.Tests";

    public const string TestConnectionString =
        "Server=127.0.0.1;Port=3317;Database=fleet_vehicle_db_test;Uid=root;Pwd=rootpassword;";

    public static string EffectiveConnectionString =>
        Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
        ?? TestConnectionString;

    public FakeVehicleEventPublisher EventPublisher { get; } = new();

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

        builder.ConfigureServices(services =>
        {
            var descriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(IVehicleEventPublisher));

            if (descriptor is not null)
            {
                services.Remove(descriptor);
            }

            services.AddSingleton<IVehicleEventPublisher>(EventPublisher);
            // Fleet tests isolate Auth as a controlled active-account dependency.
            // Revocation and unavailable Auth responses are tested separately.
            services.AddHttpClient("AuthAccountStatus", client => client.BaseAddress = new Uri("http://auth-test/"))
                .ConfigurePrimaryHttpMessageHandler(() => new ActiveDriverAuthHandler());
        });
    }
}

public class FakeVehicleEventPublisher : IVehicleEventPublisher
{
    public List<(Guid VehicleId, string PreviousStatus, string NewStatus)> Published { get; } = new();

    public Task PublishStatusChangedAsync(
        FleetVehicleService.Models.Vehicle vehicle,
        string previousStatus,
        string newStatus)
    {
        Published.Add((vehicle.Id, previousStatus, newStatus));
        return Task.CompletedTask;
    }
}
public sealed class ActiveDriverAuthHandler : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
    {
        var jwt = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler()
            .ReadJwtToken(request.Headers.Authorization!.Parameter);
        var subject = jwt.Subject;
        return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK)
        {
            Content = System.Net.Http.Json.JsonContent.Create(new { id = subject, role = jwt.Claims.First(c => c.Type == System.Security.Claims.ClaimTypes.Role || c.Type == "role").Value, isActive = true })
        });
    }
}
