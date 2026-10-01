using TripDispatchService.Data;
using TripDispatchService.DTOs;
using TripDispatchService.Models;

namespace TripDispatchService.Services;

public class TripService
{
    private const int LocationMaxLength = 255;

    private readonly ITripRepository _tripRepository;

    private readonly IFleetVehicleClient _fleetVehicleClient;

    public TripService(ITripRepository tripRepository, IFleetVehicleClient fleetVehicleClient)
    {
        _tripRepository = tripRepository;
        _fleetVehicleClient = fleetVehicleClient;
    }

    //US-09 Dispatcher creates a trip request

    public async Task<TripResponse> CreateTripAsync(CreateTripRequest request, Guid dispatcherId)
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

        // Coordinates are optional (a typed address has none), but if given,
        // must be real points on Earth - catches a swapped lat/lng or a
        // stray value slipping through from the map picker.
        if (request.PickupLat is < -90 or > 90)
        {
            errors["pickupLat"] = "Pickup latitude must be between -90 and 90.";
        }
        if (request.PickupLng is < -180 or > 180)
        {
            errors["pickupLng"] = "Pickup longitude must be between -180 and 180.";
        }
        if (request.DropoffLat is < -90 or > 90)
        {
            errors["dropoffLat"] = "Drop-off latitude must be between -90 and 90.";
        }
        if (request.DropoffLng is < -180 or > 180)
        {
            errors["dropoffLng"] = "Drop-off longitude must be between -180 and 180.";
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
            PickupLat = request.PickupLat,
            PickupLng = request.PickupLng,
            DropoffLocation = dropoff,
            DropoffLat = request.DropoffLat,
            DropoffLng = request.DropoffLng,
            RequiredCapacity = capacity,
            CreatedByDispatcherId = dispatcherId,
            Status = TripStatus.Requested,
            AssignedVehicleId = null,
            CreatedAt = now,
            UpdatedAt = now
        };

        await _tripRepository.CreateTripAsync(trip);

        return ToResponse(trip);
    }
    
    //US-13 List trips, optionally filtered to one status

    public async Task<List<TripResponse>> GetTripsAsync(string? status)
    {
        if (!string.IsNullOrWhiteSpace(status) && !TripStatus.All.Contains(status))
        {
            throw new ValidationException(new Dictionary<string, string>
            {
                ["status"] = $"Status must be one of: {string.Join(", ", TripStatus.All)}."
            });
        }

        var trips = await _tripRepository.GetAllAsync(status);

        return trips.Select(ToResponse).ToList();
    }

    // Trip Details page: fetch one trip's full information.
    public async Task<TripResponse> GetTripByIdAsync(Guid tripId)
    {
        var trip = await _tripRepository.GetByIdAsync(tripId)
            ?? throw new NotFoundException($"No trip found with id '{tripId}'.");

        return ToResponse(trip);
    }
    
    //US-10 Ranked vehicle suggestions for a trip
    public async Task<List<VehicleSuggestionResponse>> GetSuggestionsAsync(
        Guid tripId, string? authorizationHeader)
    {
        var trip = await _tripRepository.GetByIdAsync(tripId)
            ?? throw new NotFoundException($"No trip found with id '{tripId}'.");

        if (!CapacityParser.TryParse(trip.RequiredCapacity, out _, out _, out _))
        {
            // Should not happen for a trip created through this API (capacity
            // is validated on the way in), but guards against bad data.
            throw new InvalidOperationException(
                $"Trip {tripId} has an unparseable required capacity: '{trip.RequiredCapacity}'.");
        }

        return await RankVehiclesAsync(trip.RequiredCapacity, authorizationHeader);
    }

    public async Task<List<VehicleSuggestionResponse>> GetSuggestionsForCapacityAsync(
        string? requiredCapacity, string? authorizationHeader)
    {
        if (!CapacityParser.TryParse(requiredCapacity, out _, out _, out var normalized))
        {
            throw new ValidationException(new Dictionary<string, string>
            {
                ["requiredCapacity"] =
                    "Capacity must be a positive number, optionally with a unit (for example 500 kg, 8 persons, 20 units)."
            });
        }

        return await RankVehiclesAsync(normalized, authorizationHeader);
    }

    private async Task<List<VehicleSuggestionResponse>> RankVehiclesAsync(
        string requiredCapacity, string? authorizationHeader)
    {
        CapacityParser.TryParse(requiredCapacity, out var requiredAmount, out var requiredUnit, out _);

        var vehicles = await _fleetVehicleClient.GetVehiclesAsync(authorizationHeader);

        var suggestions = new List<VehicleSuggestionResponse>();

        foreach (var vehicle in vehicles)
        {
            if (!string.Equals(vehicle.Status, "Available", StringComparison.OrdinalIgnoreCase))
                continue;

            if (!CapacityParser.TryParse(vehicle.Capacity, out var vehicleAmount, out var vehicleUnit, out _))
                continue;

            // Same unit only - "5 persons" can never satisfy "500 kg".
            if (!string.Equals(vehicleUnit, requiredUnit, StringComparison.OrdinalIgnoreCase))
                continue;

            if (vehicleAmount < requiredAmount)
                continue;

            suggestions.Add(new VehicleSuggestionResponse
            {
                VehicleId = vehicle.Id,
                RegistrationNo = vehicle.RegistrationNo,
                Type = vehicle.Type,
                Capacity = vehicle.Capacity,
                Unit = vehicleUnit,
                ExcessCapacity = vehicleAmount - requiredAmount
            });
        }

        return suggestions
            .OrderBy(s => s.ExcessCapacity)
            .ToList();
    }

    // US-11: Dispatcher assigns a suggested vehicle to a trip
    // Order matters: the trip is only updated AFTER Fleet confirms the
    // vehicle really is Available and has been switched to On Trip. 

    public async Task<TripResponse> AssignVehicleAsync(
        Guid tripId, Guid? vehicleId, Guid? driverId, string? authorizationHeader)
    {
        var errors = new Dictionary<string, string>();
        if (vehicleId is null) errors["vehicleId"] = "A vehicle is required.";
        if (driverId is null) errors["driverId"] = "A driver is required.";
        if (errors.Count > 0)
        {
            throw new ValidationException(errors);
        }

        var trip = await _tripRepository.GetByIdAsync(tripId)
            ?? throw new NotFoundException($"No trip found with id '{tripId}'.");

        if (trip.Status != TripStatus.Requested)
        {
            throw new InvalidOperationException(
                $"This trip cannot be assigned because its status is '{trip.Status}' (expected '{TripStatus.Requested}').");
        }

        // Throws NotFoundException or InvalidOperationException if this fails -
        // nothing below runs, and the trip stays exactly as it was.
        await _fleetVehicleClient.SetVehicleStatusAsync(
            vehicleId!.Value, "On Trip", authorizationHeader);

        trip.AssignedVehicleId = vehicleId;
        trip.AssignedDriverId = driverId;
        trip.Status = TripStatus.Assigned;
        trip.RejectedByDriverId = null;
        trip.UpdatedAt = DateTime.UtcNow;

        await _tripRepository.UpdateAsync(trip);

        return ToResponse(trip);
    }

    //US-12 A driver responds to a trip that has been assigned to them

    public async Task<TripResponse> RespondAsync(
        Guid tripId, Guid driverId, string? decision, string? authorizationHeader)
    {
        var normalizedDecision = decision?.Trim().ToLowerInvariant();

        if (normalizedDecision != "accept" && normalizedDecision != "reject")
        {
            throw new ValidationException(new Dictionary<string, string>
            {
                ["decision"] = "Decision must be either 'accept' or 'reject'."
            });
        }

        var trip = await _tripRepository.GetByIdAsync(tripId)
            ?? throw new NotFoundException($"No trip found with id '{tripId}'.");

        if (trip.AssignedDriverId != driverId)
        {
            // Deliberately the same message as "doesn't exist" - a driver
            // should not be able to tell the difference between a trip that
            // isn't theirs and one that doesn't exist.
            throw new NotFoundException($"No trip found with id '{tripId}'.");
        }

        if (trip.Status != TripStatus.Assigned)
        {
            throw new InvalidOperationException(
                $"This trip cannot be responded to because its status is '{trip.Status}' (expected '{TripStatus.Assigned}').");
        }

                if (normalizedDecision == "accept")
        {
            trip.Status = TripStatus.InProgress;
            trip.RejectedByDriverId = null;
        }
        else
        {
            var vehicleId = trip.AssignedVehicleId;

            if (vehicleId is not null)
            {
                await _fleetVehicleClient.SetVehicleStatusAsync(
                    vehicleId.Value, "Available", authorizationHeader);
            }

            trip.Status = TripStatus.Requested;
            trip.AssignedVehicleId = null;
            trip.RejectedByDriverId = trip.AssignedDriverId;
            trip.AssignedDriverId = null;
        }

        trip.UpdatedAt = DateTime.UtcNow;

        await _tripRepository.UpdateAsync(trip);

        return ToResponse(trip);
    }

    //US-12 A driver's own trips

    public async Task<List<TripResponse>> GetMyTripsAsync(Guid driverId)
    {
        var trips = await _tripRepository.GetByDriverIdAsync(driverId);

        return trips.Select(ToResponse).ToList();
    }

    private static TripResponse ToResponse(Trip trip)
    {
        return new TripResponse
        {
            Id = trip.Id,
            PickupLocation = trip.PickupLocation,
            PickupLat = trip.PickupLat,
            PickupLng = trip.PickupLng,
            DropoffLocation = trip.DropoffLocation,
            DropoffLat = trip.DropoffLat,
            DropoffLng = trip.DropoffLng,
            RequiredCapacity = trip.RequiredCapacity,
            Status = trip.Status,
            AssignedVehicleId = trip.AssignedVehicleId,
            AssignedDriverId = trip.AssignedDriverId,
            RejectedByDriverId = trip.RejectedByDriverId,
            CreatedAt = trip.CreatedAt,
            UpdatedAt = trip.UpdatedAt
        };
    }
}