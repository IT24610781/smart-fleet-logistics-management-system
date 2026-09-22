using TripDispatchService.Data;
using TripDispatchService.DTOs;
using TripDispatchService.Models;

namespace TripDispatchService.Services;

public class TripService
{
    private const int LocationMaxLength = 255;

    private readonly ITripRepository _tripRepository;

    public TripService(ITripRepository tripRepository)
    {
        _tripRepository = tripRepository;
    }


    // US-09: Dispatcher creates a trip request

    public async Task<TripResponse> CreateTripAsync(CreateTripRequest request)
    {
        var errors = new Dictionary<string, string>();

        var pickup = request.PickupLocation?.Trim() ?? string.Empty;
        var dropoff = request.DropoffLocation?.Trim() ?? string.Empty;
        var capacity = request.RequiredCapacity?.Trim() ?? string.Empty;

        if (pickup.Length == 0)
        {
            errors["pickupLocation"] = "Pickup location is required.";
        }
        else if (pickup.Length > LocationMaxLength)
        {
            errors["pickupLocation"] =
                $"Pickup location must be {LocationMaxLength} characters or fewer.";
        }

        if (dropoff.Length == 0)
        {
            errors["dropoffLocation"] = "Drop-off location is required.";
        }
        else if (dropoff.Length > LocationMaxLength)
        {
            errors["dropoffLocation"] =
                $"Drop-off location must be {LocationMaxLength} characters or fewer.";
        }

        if (capacity.Length == 0)
        {
            errors["requiredCapacity"] = "Required capacity is missing.";
        }
        else if (!CapacityParser.TryParse(capacity, out _, out _, out var normalizedCapacity))
        {
            errors["requiredCapacity"] =
                "Capacity must be a positive number, optionally with a unit (for example 500 kg, 8 persons, 20 units).";
        }
        else
        {
            // Store one consistent format: "500kg" and "500  kg" both become "500 kg".
            capacity = normalizedCapacity;
        }

        // Nothing is saved unless EVERY field is valid (Scenario 2).
        if (errors.Count > 0)
        {
            throw new ValidationException(errors);
        }

        var now = DateTime.UtcNow;

        // The status is decided here. It is never read from the request, so a
        // client cannot create a trip that is already "Assigned" or "Completed".
        var trip = new Trip
        {
            Id = Guid.CreateVersion7(),
            PickupLocation = pickup,
            DropoffLocation = dropoff,
            RequiredCapacity = capacity,
            Status = TripStatus.Requested,
            AssignedVehicleId = null,
            CreatedAt = now,
            UpdatedAt = now
        };

        await _tripRepository.CreateTripAsync(trip);

        return ToResponse(trip);
    }


    private static TripResponse ToResponse(Trip trip)
    {
        return new TripResponse
        {
            Id = trip.Id,
            PickupLocation = trip.PickupLocation,
            DropoffLocation = trip.DropoffLocation,
            RequiredCapacity = trip.RequiredCapacity,
            Status = trip.Status,
            AssignedVehicleId = trip.AssignedVehicleId,
            CreatedAt = trip.CreatedAt,
            UpdatedAt = trip.UpdatedAt
        };
    }
}