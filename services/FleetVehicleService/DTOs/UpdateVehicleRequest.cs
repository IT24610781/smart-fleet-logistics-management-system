namespace FleetVehicleService.DTOs;

// =======US-06: Admin updates a vehicle's details and status.

public class UpdateVehicleRequest
{
    public string RegistrationNo { get; set; } = string.Empty;

    public string Type { get; set; } = string.Empty;

    public string Capacity { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;
}