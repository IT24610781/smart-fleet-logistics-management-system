using TripDispatchService.Models;

namespace TripDispatchService.Data;

// The service talks to this interface, so its rules can be tested with an
// in-memory stand-in (no database needed, which is also how CI runs them).
public interface ITripRepository
{
    Task CreateTripAsync(Trip trip);
// status == null means "all trips".
    Task<List<Trip>> GetAllAsync(string? status);

// US-10: look up a single trip (used by the vehicle-suggestions endpoint).
    Task<Trip?> GetByIdAsync(Guid id);
 
}