namespace TripDispatchService.DTOs;

public class TripResponse
{
    public Guid Id { get; set; }

    public string PickupLocation { get; set; } = string.Empty;

    public string DropoffLocation { get; set; } = string.Empty;

    public string RequiredCapacity { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public Guid? AssignedVehicleId { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }
}