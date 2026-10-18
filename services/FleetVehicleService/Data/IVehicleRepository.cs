using FleetVehicleService.Models;

namespace FleetVehicleService.Data;

// Persistence boundary: production uses MySQL; unit tests use a recording fake.
public interface IVehicleRepository
{
    Task CreateVehicleAsync(Vehicle vehicle);
    Task<Vehicle?> GetByRegistrationNoAsync(string registrationNo);
    Task<Vehicle?> GetByIdAsync(Guid id);
    Task UpdateVehicleAsync(Vehicle vehicle);
    Task<List<Vehicle>> GetAllAsync();
    Task DeleteVehicleAsync(Guid id);
}
