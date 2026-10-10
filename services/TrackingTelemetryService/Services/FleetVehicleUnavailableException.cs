namespace TrackingTelemetryService.Services;

public class FleetServiceUnavailableException(string message, Exception inner) : Exception(message, inner);