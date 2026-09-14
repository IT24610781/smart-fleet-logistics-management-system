namespace FleetVehicleService.Models;

// ============================================================
// US-04/US-06: Vehicle status values
// Must match the CHECK constraint in database/fleet-vehicle/schema.sql
// ============================================================

public static class VehicleStatus
{
    public const string Available = "Available";

    public const string OnTrip = "On Trip";

    public const string UnderMaintenance = "Under Maintenance";

    public static readonly string[] All =
    {
        Available,
        OnTrip,
        UnderMaintenance
    };
}