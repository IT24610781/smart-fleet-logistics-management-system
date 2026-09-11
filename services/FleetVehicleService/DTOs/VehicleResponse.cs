namespace FleetVehicleService.DTOs;

public class VehicleResponse
{
    public Guid Id { get; set; }

    public string RegistrationNo { get; set; } = string.Empty;

    public string Type { get; set; } = string.Empty;

    public int Capacity { get; set; }

    public string Status { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }
}
