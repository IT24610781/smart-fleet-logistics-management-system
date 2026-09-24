namespace TripDispatchService.DTOs;

// US-10:vehicle has beyond the trip's requirement - the smaller, the better a fit.
public class VehicleSuggestionResponse
{
    public Guid VehicleId { get; set; }

    public string RegistrationNo { get; set; } = string.Empty;

    public string Type { get; set; } = string.Empty;

    public string Capacity { get; set; } = string.Empty;

    public decimal ExcessCapacity { get; set; }

    public string Unit { get; set; } = string.Empty;
}