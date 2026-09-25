namespace TripDispatchService.Models;

public class Trip
{
    public Guid Id { get; set; }

    public string PickupLocation { get; set; } = string.Empty;

    public string DropoffLocation { get; set; } = string.Empty;

    public string RequiredCapacity { get; set; } = string.Empty;

    public string Status { get; set; } = TripStatus.Requested;

    public Guid? AssignedVehicleId { get; set; }

    public Guid? AssignedDriverId { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }
}