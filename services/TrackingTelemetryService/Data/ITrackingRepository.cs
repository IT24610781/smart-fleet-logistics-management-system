using TrackingTelemetryService.Models;

namespace TrackingTelemetryService.Data;

public interface ITrackingRepository
{
    Task CreateAsync(TripLocationUpdate update);
    Task<List<TripLocationUpdate>> GetByTripIdAsync(Guid tripId, int limit);

    // One row per trip id: that trip's single most-recent reading. A trip
    // with no readings at all is simply absent from the result dictionary.
    Task<Dictionary<Guid, TripLocationUpdate>> GetLatestPerTripAsync(List<Guid> tripIds);
}