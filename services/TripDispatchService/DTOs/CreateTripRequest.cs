namespace TripDispatchService.DTOs;

public class CreateTripRequest
{
    public string? PickupLocation { get; set; }

    public decimal? PickupLat { get; set; }
    public decimal? PickupLng { get; set; }

    public string? DropoffLocation { get; set; }

    public decimal? DropoffLat { get; set; }
    public decimal? DropoffLng { get; set; }

    public string? RequiredCapacity { get; set; }
}