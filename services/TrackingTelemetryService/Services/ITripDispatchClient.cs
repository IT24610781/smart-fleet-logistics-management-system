namespace TrackingTelemetryService.Services;

// Just the fields this service actually needs from Trip's GET /api/trips/{id}
// response - extra fields (pickup location, capacity, etc.) are ignored
// automatically by System.Text.Json's deserialization.
public record TripSnapshot(Guid Id, string Status, Guid? AssignedDriverId, Guid? AssignedVehicleId);
<<<<<<< Updated upstream
=======
public record FleetTripSnapshot(
    Guid Id, string Status, Guid? AssignedVehicleId, Guid? AssignedDriverId,
    string PickupLocation, string DropoffLocation);
public record CompletedTripSnapshot(
    Guid Id, Guid? AssignedVehicleId, DateTime CreatedAt, DateTime UpdatedAt);
>>>>>>> Stashed changes

public interface ITripDispatchClient
{
    // Returns null if the trip does not exist - mirrors a 404 from Trip,
    // never throws for "not found", only for a genuine service failure.
    Task<TripSnapshot?> GetTripAsync(Guid tripId, string? authorizationHeader, CancellationToken cancellationToken);
<<<<<<< Updated upstream
=======

    // SCRUM-128: the set of currently In-Progress trips, used to scope the
    // fleet map to active vehicles only.
    Task<List<FleetTripSnapshot>> GetInProgressTripsAsync(string? authorizationHeader, CancellationToken cancellationToken);

    // SCRUM-138: every Completed trip, used by the fleet-utilization report.
    Task<List<CompletedTripSnapshot>> GetCompletedTripsAsync(string? authorizationHeader, CancellationToken cancellationToken);
>>>>>>> Stashed changes
}