namespace TripDispatchService.DTOs;

public class AssignVehicleRequest
{
    public Guid? VehicleId { get; set; }

    //US-12 Dispatcher picks the driver at the same time as the
    // vehicle. The driver then accepts or rejects this specific offer.
    public Guid? DriverId { get; set; }
}