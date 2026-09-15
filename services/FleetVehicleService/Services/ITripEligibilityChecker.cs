namespace FleetVehicleService.Services;

// ============================================================
// US-07 Scenario 2: a vehicle with an active or upcoming trip.
// ============================================================

public interface ITripEligibilityChecker
{
    Task<bool> HasActiveOrUpcomingTripsAsync(Guid vehicleId);
}

// ============================================================
// Temporary stub: always reports no active/upcoming trips.
// ============================================================

public class StubTripEligibilityChecker : ITripEligibilityChecker
{
    private readonly ILogger<StubTripEligibilityChecker> _logger;

    public StubTripEligibilityChecker(ILogger<StubTripEligibilityChecker> logger)
    {
        _logger = logger;
    }

    public Task<bool> HasActiveOrUpcomingTripsAsync(Guid vehicleId)
    {
        _logger.LogInformation(
            "[TripDispatchService stub] Assuming vehicle {VehicleId} " +
            "has no active/upcoming trips - real check not wired yet.",
            vehicleId);

        return Task.FromResult(false);
    }
}