namespace TrackingTelemetryService.DTOs;

// SCRUM-138: Response for GET /api/reports/fleet-utilization.

public class FleetUtilizationResponse
{
    public DateTime PeriodStart { get; set; }
    public DateTime PeriodEnd { get; set; }
    public List<VehicleUtilizationEntry> Vehicles { get; set; } = [];
    public FleetUtilizationSummary Summary { get; set; } = new();
}

public class VehicleUtilizationEntry
{
    public Guid VehicleId { get; set; }
    public string RegistrationNo { get; set; } = string.Empty;
    public string VehicleType { get; set; } = string.Empty;

    // Rounded to 2dp, capped at 100 even if clipped pings somehow overshoot
    // the window by a few seconds of clock skew.
    public double UtilizationPercent { get; set; }

    public double InUseHours { get; set; }

    // Completed trips assigned to this vehicle that overlap the period,
    // regardless of whether usable location data was found for them.
    public int CompletedTripCount { get; set; }

    // True when at least one of this vehicle's completed trips in the
    // period had fewer than two accepted location pings, so its true
    // in-use time could not be measured and InUseHours understates it.
    // AC4: "the limitation is shown" rather than quietly rounding to 0%.
    public bool HasInsufficientData { get; set; }
}

public class FleetUtilizationSummary
{
    public int TotalVehicles { get; set; }
    public double TotalInUseHours { get; set; }
    public double TotalPossibleHours { get; set; }

    // TotalInUseHours / TotalPossibleHours, not an average of the
    // per-vehicle percentages - so one idle vehicle in a 50-vehicle fleet
    // doesn't pull the summary down as hard as it would pull a plain mean.
    public double OverallUtilizationPercent { get; set; }

    public int VehiclesWithInsufficientData { get; set; }
}