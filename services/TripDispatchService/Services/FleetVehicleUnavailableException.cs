namespace TripDispatchService.Services;

public sealed class FleetVehicleUnavailableException : Exception
{
    public FleetVehicleUnavailableException(string message, Exception? innerException = null)
        : base(message, innerException) { }
}
