namespace FleetVehicleService.Services;

// ============================================================
// Carries one or more field-level validation errors, so the API
// can return a 400 Bad Request that lists every invalid field
// at once (AC Scenario 3: "listing specific validation errors"),
// rather than stopping at the first one.
// ============================================================

public class ValidationException : Exception
{
    public Dictionary<string, string> Errors { get; }

    public ValidationException(Dictionary<string, string> errors)
        : base("One or more validation errors occurred.")
    {
        Errors = errors;
    }
}
