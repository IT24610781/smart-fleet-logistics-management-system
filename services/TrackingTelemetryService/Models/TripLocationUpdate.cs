namespace TrackingTelemetryService.Models;

public class TripLocationUpdate
{
    public Guid Id { get; set; }

    public Guid TripId { get; set; }
    public Guid DriverId { get; set; }
    public Guid VehicleId { get; set; }

    public decimal Latitude { get; set; }
    public decimal Longitude { get; set; }
    public DateTime ObservedAt { get; set; }
    public DateTime ReceivedAt { get; set; }
}