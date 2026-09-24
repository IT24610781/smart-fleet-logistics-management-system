namespace TripDispatchService.Services;

// Carries one or more field-level validation errors, so the API can return a
public class ValidationException : Exception
{
    public Dictionary<string, string> Errors { get; }

    public ValidationException(Dictionary<string, string> errors)
        : base("One or more validation errors occurred.")
    {
        Errors = errors;
    }
}