namespace FleetVehicleService.Models;

public class Vehicle
{
    public Guid Id { get; set; }

    public string RegistrationNo { get; set; } = string.Empty;

    public string Type { get; set; } = string.Empty;

    public string Capacity { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }
}
