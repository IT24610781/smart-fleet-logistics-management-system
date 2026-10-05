namespace TrackingTelemetryService.Services;

// Just the fields this service actually needs from Trip's GET /api/trips/{id}
// response - extra fields (pickup location, capacity, etc.) are ignored
// automatically by System.Text.Json's deserialization.
public record TripSnapshot(Guid Id, string Status, Guid? AssignedDriverId, Guid? AssignedVehicleId);

public interface ITripDispatchClient
{
    // Returns null if the trip does not exist - mirrors a 404 from Trip,
    // never throws for "not found", only for a genuine service failure.
    Task<TripSnapshot?> GetTripAsync(Guid tripId, string? authorizationHeader, CancellationToken cancellationToken);
}