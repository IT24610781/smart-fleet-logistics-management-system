namespace FleetVehicleService.DTOs;

// ============================================================
// US-04: Admin registers a vehicle
// Capacity is intentionally a string here (not int) so that a
// non-numeric value ("abc") reaches our own validation code and
// produces a clear, listed error message - rather than failing
// silently at model-binding with .NET's generic type-mismatch
// response, which AC Scenario 3 doesn't ask for.
// ============================================================

public class CreateVehicleRequest
{
    public string RegistrationNo { get; set; } = string.Empty;

    public string Type { get; set; } = string.Empty;

    public string Capacity { get; set; } = string.Empty;
}
