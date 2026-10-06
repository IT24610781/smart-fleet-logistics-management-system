using TrackingTelemetryService.Models;

namespace TrackingTelemetryService.Data;

public interface ITrackingRepository
{
    Task CreateAsync(TripLocationUpdate update);

    // Ordered by ObservedAt ascending (real-world time the reading
    // happened), capped at the given limit.
    Task<List<TripLocationUpdate>> GetByTripIdAsync(Guid tripId, int limit);
}