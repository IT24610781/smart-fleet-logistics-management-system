using TrackingTelemetryService.Data;
using TrackingTelemetryService.DTOs;

namespace TrackingTelemetryService.Services;

// SCRUM-137/138: Fleet Utilization Report (US-17).

//   - "Utilization" means TIME IN USE, not trip count. A vehicle's current
//     status alone can't establish this historically, so this is
//     calculated from actual records, never from Vehicle.Status "now".
//   - There is no trip_status_history table, so the exact moment a trip
//     became In-Progress isn't stored directly. A Driver only submits
//     location pings while their trip is In-Progress (enforced in
//     TrackingService.SubmitLocationAsync), so the span between a
//     Completed trip's first and last accepted ping is used as a real,
//     data-backed proxy for its in-use duration.
//   - Per vehicle: sum each eligible Completed trip's ping span, CLIPPED to
//     the requested period (AC Scenario 2), then
//     UtilizationPercent = InUseHours / PeriodHours * 100, capped at 100.
//   - Eligible vehicles = every vehicle registered on/before the period
//     end, regardless of current status (a vehicle now Under Maintenance
//     may still have been active earlier in the window). A vehicle with
//     zero qualifying trips gets a real 0%, not "unavailable".
//   - A trip with fewer than two accepted pings has no measurable span, so
//     it contributes 0 hours but flags the vehicle's HasInsufficientData -
//     AC Scenario 4 requires the limitation to be shown, not silently
//     folded into a (falsely precise-looking) 0%.
//   - Fleet summary = total in-use hours / total possible hours across all
//     eligible vehicles - not a plain average of the per-vehicle
//     percentages, so one idle vehicle in a large fleet doesn't distort
//     the summary as much as it would distort a mean of percentages.
public class FleetUtilizationService(
    IFleetVehicleClient fleetClient,
    ITripDispatchClient tripClient,
    ITrackingRepository trackingRepository)
{
    // GetByTripIdAsync's own cap. Only the first and last ping matter here,
    // so this assumes no single trip generates more than 1000 accepted
    // pings - comfortably true for the ~15s polling interval the Driver
    // app uses, up to several hours per trip.
    private const int HistoryLimit = 1000;

    public async Task<FleetUtilizationResponse> GetFleetUtilizationAsync(
        DateTime? start,
        DateTime? end,
        string? authorizationHeader,
        CancellationToken cancellationToken)
    {
        var errors = new Dictionary<string, string>();

        if (start is null)
        {
            errors["start"] = "Start date is required.";
        }

        if (end is null)
        {
            errors["end"] = "End date is required.";
        }

        // AC Scenario 3. Treating an equal start/end as invalid too - a
        // zero-length window has no meaningful "period" to report on -
        // rather than only rejecting the literal "end before start" case.
        if (start is not null && end is not null && end <= start)
        {
            errors["end"] = "End date must be after the start date.";
        }

        if (errors.Count > 0)
        {
            throw new ValidationException(errors);
        }

        var periodStart = start!.Value;
        var periodEnd = end!.Value;
        var periodHours = (periodEnd - periodStart).TotalHours;

        var vehicles = await fleetClient.GetAllVehiclesAsync(authorizationHeader, cancellationToken);
        var eligibleVehicles = vehicles.Where(v => v.CreatedAt <= periodEnd).ToList();

        var completedTrips = await tripClient.GetCompletedTripsAsync(authorizationHeader, cancellationToken);

        // Safe pre-filter: a trip's full creation-to-completion lifecycle
        // always encloses its real In-Progress ping span, so anything that
        // can't possibly overlap the window is skipped before the
        // per-trip history lookup.
        var candidateTripsByVehicle = completedTrips
            .Where(t => t.AssignedVehicleId is not null
                && t.UpdatedAt >= periodStart
                && t.CreatedAt <= periodEnd)
            .GroupBy(t => t.AssignedVehicleId!.Value)
            .ToDictionary(g => g.Key, g => g.ToList());

        var entries = new List<VehicleUtilizationEntry>();

        foreach (var vehicle in eligibleVehicles)
        {
            var entry = new VehicleUtilizationEntry
            {
                VehicleId = vehicle.Id,
                RegistrationNo = vehicle.RegistrationNo,
                VehicleType = vehicle.Type,
            };

            if (candidateTripsByVehicle.TryGetValue(vehicle.Id, out var trips))
            {
                entry.CompletedTripCount = trips.Count;

                double inUseHours = 0;
                var insufficientData = false;

                foreach (var trip in trips)
                {
                    var pings = await trackingRepository.GetByTripIdAsync(trip.Id, HistoryLimit);

                    if (pings.Count < 2)
                    {
                        // One ping (or none) can't establish a duration.
                        insufficientData = true;
                        continue;
                    }

                    var first = pings.Min(p => p.ObservedAt);
                    var last = pings.Max(p => p.ObservedAt);

                    var clippedStart = first > periodStart ? first : periodStart;
                    var clippedEnd = last < periodEnd ? last : periodEnd;

                    var hours = (clippedEnd - clippedStart).TotalHours;
                    if (hours > 0)
                    {
                        inUseHours += hours;
                    }
                }

                entry.InUseHours = Math.Round(inUseHours, 2);
                entry.HasInsufficientData = insufficientData;
                entry.UtilizationPercent = periodHours > 0
                    ? Math.Round(Math.Min(100, inUseHours / periodHours * 100), 2)
                    : 0;
            }

            entries.Add(entry);
        }

        var totalInUseHours = entries.Sum(e => e.InUseHours);
        var totalPossibleHours = eligibleVehicles.Count * periodHours;

        return new FleetUtilizationResponse
        {
            PeriodStart = periodStart,
            PeriodEnd = periodEnd,
            Vehicles = entries.OrderByDescending(e => e.UtilizationPercent).ToList(),
            Summary = new FleetUtilizationSummary
            {
                TotalVehicles = eligibleVehicles.Count,
                TotalInUseHours = Math.Round(totalInUseHours, 2),
                TotalPossibleHours = Math.Round(totalPossibleHours, 2),
                OverallUtilizationPercent = totalPossibleHours > 0
                    ? Math.Round(Math.Min(100, totalInUseHours / totalPossibleHours * 100), 2)
                    : 0,
                VehiclesWithInsufficientData = entries.Count(e => e.HasInsufficientData),
            }
        };
    }
}