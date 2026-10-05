namespace TrackingTelemetryService.DTOs;

public class LocationUpdateResponse
{
    public Guid Id { get; set; }
    public Guid TripId { get; set; }
    public decimal Latitude { get; set; }
    public decimal Longitude { get; set; }
    public DateTime ObservedAt { get; set; }
    public DateTime ReceivedAt { get; set; }
}