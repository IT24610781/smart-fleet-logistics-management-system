namespace TripDispatchService.DTOs;

public class TripResponse
{
    public Guid Id { get; set; }

    public string PickupLocation { get; set; } = string.Empty;

    public decimal? PickupLat { get; set; }
    public decimal? PickupLng { get; set; }

    public string DropoffLocation { get; set; } = string.Empty;

    public decimal? DropoffLat { get; set; }
    public decimal? DropoffLng { get; set; }

    public string RequiredCapacity { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public Guid? AssignedVehicleId { get; set; }

    public Guid? AssignedDriverId { get; set; }

    public Guid? RejectedByDriverId { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }
}