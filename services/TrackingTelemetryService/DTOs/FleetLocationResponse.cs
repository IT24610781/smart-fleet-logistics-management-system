namespace TrackingTelemetryService.DTOs;

public class FleetLocationResponse
{
    public Guid TripId { get; set; }
    public Guid? VehicleId { get; set; }
    public Guid? DriverId { get; set; }
    public string PickupLocation { get; set; } = string.Empty;
    public string DropoffLocation { get; set; } = string.Empty;
    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }
    public DateTime? ObservedAt { get; set; }
    public string Freshness { get; set; } = string.Empty;
}