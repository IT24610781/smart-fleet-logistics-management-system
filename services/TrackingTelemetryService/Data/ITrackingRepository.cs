using TrackingTelemetryService.Models;

namespace TrackingTelemetryService.Data;

public interface ITrackingRepository
{
    Task CreateAsync(TripLocationUpdate update);
}