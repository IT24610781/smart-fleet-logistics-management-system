namespace TripDispatchService.DTOs;

public class CreateTripRequest
{
    public string? PickupLocation { get; set; }

    public string? DropoffLocation { get; set; }

    public string? RequiredCapacity { get; set; }
}