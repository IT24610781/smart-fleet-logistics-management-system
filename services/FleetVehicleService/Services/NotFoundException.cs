namespace FleetVehicleService.Services;

// =======US-06: Thrown when an Admin tries to update a vehicle

public class NotFoundException : Exception
{
    public NotFoundException(string message) : base(message)
    {
    }
}
