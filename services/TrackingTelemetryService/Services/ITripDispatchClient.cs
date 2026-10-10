namespace TrackingTelemetryService.Services;
public record TripSnapshot(Guid Id, string Status, Guid? AssignedDriverId, Guid? AssignedVehicleId);
public record FleetTripSnapshot(
    Guid Id, string Status, Guid? AssignedVehicleId, Guid? AssignedDriverId,
    string PickupLocation, string DropoffLocation);

public interface ITripDispatchClient
{
    // Returns null if the trip does not exist - mirrors a 404 from Trip,
    // never throws for "not found", only for a genuine service failure.
    Task<TripSnapshot?> GetTripAsync(Guid tripId, string? authorizationHeader, CancellationToken cancellationToken);

    // SCRUM-128: the set of currently In-Progress trips, used to scope the
    // fleet map to active vehicles only.
    Task<List<FleetTripSnapshot>> GetInProgressTripsAsync(string? authorizationHeader, CancellationToken cancellationToken);
}