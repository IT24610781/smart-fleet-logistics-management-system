namespace TripDispatchService.Models;

public class Trip
{
    public Guid Id { get; set; }

    public string PickupLocation { get; set; } = string.Empty;

    // Optional: precise coordinates captured via GPS or a dropped map pin.
    // Null when the Dispatcher just typed a plain address.
    public decimal? PickupLat { get; set; }
    public decimal? PickupLng { get; set; }

    public string DropoffLocation { get; set; } = string.Empty;

    public decimal? DropoffLat { get; set; }
    public decimal? DropoffLng { get; set; }

    public string RequiredCapacity { get; set; } = string.Empty;

    public string Status { get; set; } = TripStatus.Requested;

    public Guid? AssignedVehicleId { get; set; }

    public Guid? AssignedDriverId { get; set; }

    public Guid? RejectedByDriverId { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }
}