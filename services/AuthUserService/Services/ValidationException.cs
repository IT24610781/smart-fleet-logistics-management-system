namespace AuthUserService.Services;

// ============================================================
// US-08: Carries one or more field-level validation errors, so the API
// can return a 400 Bad Request that lists every invalid field at once
// (AC Scenario 2: "clear field-level validation errors"), rather than
// stopping at the first one.
//
// Same idea as FleetVehicleService.Services.ValidationException.
// Keys are the camelCase field names the frontend form uses
// (username, email, password, firstName, lastName, contactNumber).
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
