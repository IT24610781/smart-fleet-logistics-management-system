namespace TripDispatchService.Services;

// Thrown when a trip id doesn't exist
public class NotFoundException : Exception
{
    public NotFoundException(string message) : base(message)
    {
    }
}