namespace FleetVehicleService.DTOs;

// US-11 Dispatcher starts or ends a vehicle's use on a trip.
// Deliberately narrower than the full UpdateVehicleRequest (Admin-only) -
// this only ever moves a vehicle between Available and On Trip.
public class UpdateVehicleStatusRequest
{
    public string? Status { get; set; }
}