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