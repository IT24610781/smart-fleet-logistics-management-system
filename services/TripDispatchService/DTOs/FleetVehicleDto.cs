namespace TripDispatchService.DTOs;

// Shape of one item from Fleet's GET /api/vehicles response. Only the
// fields the matching logic actually needs.
public class FleetVehicleDto
{
    public Guid Id { get; set; }

    public string RegistrationNo { get; set; } = string.Empty;

    public string Type { get; set; } = string.Empty;

    public string Capacity { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;
}