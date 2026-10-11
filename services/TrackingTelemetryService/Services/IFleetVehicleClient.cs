namespace TrackingTelemetryService.Services;

// SCRUM-138: just enough of a vehicle for the fleet-utilization report.
public record FleetVehicleSnapshot(Guid Id, string RegistrationNo, string Type, DateTime CreatedAt);

public interface IFleetVehicleClient
{
    // All registered vehicles, regardless of current status - a vehicle
    // that is now Under Maintenance may still have been active earlier in
    // the reporting period, so "current status" can't be used to filter.
    Task<List<FleetVehicleSnapshot>> GetAllVehiclesAsync(string? authorizationHeader, CancellationToken cancellationToken);
}