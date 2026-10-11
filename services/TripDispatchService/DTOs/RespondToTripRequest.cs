namespace TripDispatchService.DTOs;

public class RespondToTripRequest
{
    // Expected values: "accept" or "reject" (case-insensitive).
    public string? Decision { get; set; }
}