namespace TrackingTelemetryService.Services;

public class NotFoundException(string message) : Exception(message);