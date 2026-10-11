namespace TrackingTelemetryService.DTOs;

public class SubmitLocationRequest
{
    public Guid? TripId { get; set; }
    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }
    public DateTime? ObservedAt { get; set; }
}