using TripDispatchService.DTOs;

namespace TripDispatchService.Services;

// Talks to the Fleet service over HTTP. An interface so TripService's
public interface IFleetVehicleClient
{
    Task<List<FleetVehicleDto>> GetVehiclesAsync(string? authorizationHeader);

    // US-11: PATCH /api/vehicles/{id}/status on Fleet. Throws NotFoundException
    Task SetVehicleStatusAsync(Guid vehicleId, string status, string? authorizationHeader);

}