namespace TrackingTelemetryService.Services;

public class TripServiceUnavailableException(string message, Exception inner) : Exception(message, inner);