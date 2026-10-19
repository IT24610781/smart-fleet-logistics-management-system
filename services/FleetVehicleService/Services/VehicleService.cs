using FleetVehicleService.Data;
using FleetVehicleService.DTOs;
using FleetVehicleService.Models;

namespace FleetVehicleService.Services;

public class VehicleService
{
    private readonly IVehicleRepository _vehicleRepository;
    private readonly IVehicleEventPublisher _eventPublisher;
    private readonly ITripEligibilityChecker _tripEligibilityChecker;

    // SCRUM-86: must match the column sizes in database/fleet-vehicle/schema.sql
    // (registration_no VARCHAR(20), type VARCHAR(50)). Validating here turns
    // what used to be a raw MySQL "Data too long" 500 into a clean 400.
    private const int RegistrationNoMaxLength = 20;
    private const int TypeMaxLength = 50;

    public VehicleService(
        IVehicleRepository vehicleRepository,
        IVehicleEventPublisher eventPublisher,
        ITripEligibilityChecker tripEligibilityChecker)
    {
        _vehicleRepository = vehicleRepository;
        _eventPublisher = eventPublisher;
        _tripEligibilityChecker = tripEligibilityChecker;
    }

    // US-04: Admin registers a vehicle

    public async Task<VehicleResponse> CreateVehicleAsync(
    CreateVehicleRequest request)
{
    var errors = new Dictionary<string, string>();

    var registrationNo = request.RegistrationNo?.Trim() ?? string.Empty;
    var type = request.Type?.Trim() ?? string.Empty;

    if (string.IsNullOrWhiteSpace(registrationNo))
    {
        errors["registrationNo"] = "Registration number is required.";
    }
    else if (registrationNo.Length > RegistrationNoMaxLength)
    {
        errors["registrationNo"] =
            $"Registration number must be {RegistrationNoMaxLength} characters or fewer.";
    }

    if (string.IsNullOrWhiteSpace(type))
    {
        errors["type"] = "Vehicle type is required.";
    }
    else if (type.Length > TypeMaxLength)
    {
        errors["type"] =
            $"Vehicle type must be {TypeMaxLength} characters or fewer.";
    }

    var capacity = request.Capacity?.Trim() ?? string.Empty;

    if (string.IsNullOrWhiteSpace(capacity))
    {
        errors["capacity"] = "Capacity is required.";
    }
    else if (!CapacityParser.TryParse(capacity, out _, out _, out var normalizedCapacity))
    {
        errors["capacity"] =
            "Capacity must be a positive number, optionally with a unit (for example 500 kg, 8 persons, 20 units).";
    }
    else
    {
    // Store one consistent format: "500kg" and "500  kg" both become "500 kg".
        capacity = normalizedCapacity;
    }

    if (errors.Count > 0)
    {
        throw new ValidationException(errors);
    }

    // AC Scenario 2: duplicate registration numbers are rejected
    var existing =
        await _vehicleRepository.GetByRegistrationNoAsync(registrationNo);

    if (existing is not null)
    {
        throw new InvalidOperationException(
            $"A vehicle with registration number '{registrationNo}' already exists.");
    }

    var vehicle = new Vehicle
    {
        Id = Guid.NewGuid(),
        RegistrationNo = registrationNo,
        Type = type,
        Capacity = capacity,
        Status = VehicleStatus.Available
    };

    await _vehicleRepository.CreateVehicleAsync(vehicle);

    return new VehicleResponse
    {
        Id = vehicle.Id,
        RegistrationNo = vehicle.RegistrationNo,
        Type = vehicle.Type,
        Capacity = vehicle.Capacity,
        Status = vehicle.Status,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
    };
}

    // US-06: Admin updates a vehicle's details and status

    public async Task<VehicleResponse> UpdateVehicleAsync(
    Guid id, UpdateVehicleRequest request)
{
    var vehicle = await _vehicleRepository.GetByIdAsync(id);

    if (vehicle is null)
    {
        throw new NotFoundException(
            $"No vehicle found with id '{id}'.");
    }

    var errors = new Dictionary<string, string>();

    var registrationNo = request.RegistrationNo?.Trim() ?? string.Empty;

    if (string.IsNullOrWhiteSpace(registrationNo))
    {
        errors["registrationNo"] = "Registration number is required.";
    }
    else if (registrationNo.Length > RegistrationNoMaxLength)
    {
        errors["registrationNo"] =
            $"Registration number must be {RegistrationNoMaxLength} characters or fewer.";
    }

    var type = request.Type?.Trim() ?? string.Empty;

    if (string.IsNullOrWhiteSpace(type))
    {
        errors["type"] = "Vehicle type is required.";
    }
    else if (type.Length > TypeMaxLength)
    {
        errors["type"] =
            $"Vehicle type must be {TypeMaxLength} characters or fewer.";
    }

    var capacity = request.Capacity?.Trim() ?? string.Empty;

    if (string.IsNullOrWhiteSpace(capacity))
    {
        errors["capacity"] = "Capacity is required.";
    }
    else if (!CapacityParser.TryParse(capacity, out _, out _, out var normalizedCapacity))
    {
        errors["capacity"] =
            "Capacity must be a positive number, optionally with a unit (for example 500 kg, 8 persons, 20 units).";
    }
    else
    {
    // Store one consistent format: "500kg" and "500  kg" both become "500 kg".
        capacity = normalizedCapacity;
    }

    var status = request.Status?.Trim() ?? string.Empty;

    if (string.IsNullOrWhiteSpace(status))
    {
        errors["status"] = "Status is required.";
    }
    else if (!VehicleStatus.All.Contains(status))
    {
        errors["status"] =
            $"Status must be one of: {string.Join(", ", VehicleStatus.All)}.";
    }

    if (errors.Count > 0)
    {
        throw new ValidationException(errors);
    }

    if (!string.Equals(registrationNo, vehicle.RegistrationNo, StringComparison.OrdinalIgnoreCase))
    {
        var existing =
            await _vehicleRepository.GetByRegistrationNoAsync(registrationNo);

        if (existing is not null && existing.Id != id)
        {
            throw new InvalidOperationException(
                $"A vehicle with registration number '{registrationNo}' already exists.");
        }
    }

    vehicle.RegistrationNo = registrationNo;
    vehicle.Type = type;
    vehicle.Capacity = capacity;

    var previousStatus = vehicle.Status;
    vehicle.Status = status;

    await _vehicleRepository.UpdateVehicleAsync(vehicle);

    if (previousStatus != status)
    {
        await _eventPublisher.PublishStatusChangedAsync(
            vehicle, previousStatus, status);
    }

    return new VehicleResponse
    {
        Id = vehicle.Id,
        RegistrationNo = vehicle.RegistrationNo,
        Type = vehicle.Type,
        Capacity = vehicle.Capacity,
        Status = vehicle.Status,
        CreatedAt = vehicle.CreatedAt,
        UpdatedAt = DateTime.UtcNow
    };
}
    
    // US-11 Dispatcher starts/ends a vehicle's use on a trip
    // Narrow on purpose: only Available <-> On Trip, and only when the
    // vehicle is actually in the state that transition expects.
   
    public async Task<VehicleResponse> UpdateVehicleStatusAsync(Guid id, string? status)
    {
        var vehicle = await _vehicleRepository.GetByIdAsync(id);

        if (vehicle is null)
        {
            throw new NotFoundException(
                $"No vehicle found with id '{id}'.");
        }

        var trimmedStatus = status?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(trimmedStatus))
        {
            throw new ValidationException(new Dictionary<string, string>
            {
                ["status"] = "Status is required."
            });
        }

        if (trimmedStatus != VehicleStatus.OnTrip && trimmedStatus != VehicleStatus.Available)
        {
            throw new ValidationException(new Dictionary<string, string>
            {
                ["status"] = $"Status must be one of: {VehicleStatus.OnTrip}, {VehicleStatus.Available}."
            });
        }

        if (trimmedStatus == VehicleStatus.OnTrip && vehicle.Status != VehicleStatus.Available)
        {
            throw new InvalidOperationException(
                $"Vehicle '{vehicle.RegistrationNo}' is not Available (current status: {vehicle.Status}).");
        }

        if (trimmedStatus == VehicleStatus.Available && vehicle.Status != VehicleStatus.OnTrip)
        {
            throw new InvalidOperationException(
                $"Vehicle '{vehicle.RegistrationNo}' is not currently On Trip (current status: {vehicle.Status}).");
        }

        var previousStatus = vehicle.Status;
        vehicle.Status = trimmedStatus;

        await _vehicleRepository.UpdateVehicleAsync(vehicle);

        await _eventPublisher.PublishStatusChangedAsync(
            vehicle, previousStatus, trimmedStatus);

        return new VehicleResponse
        {
            Id = vehicle.Id,
            RegistrationNo = vehicle.RegistrationNo,
            Type = vehicle.Type,
            Capacity = vehicle.Capacity,
            Status = vehicle.Status,
            CreatedAt = vehicle.CreatedAt,
            UpdatedAt = DateTime.UtcNow
        };
    }

    // US-05 Dispatcher/Admin views all registered vehicles

    public async Task<List<VehicleResponse>> GetAllVehiclesAsync()
    {
        var vehicles = await _vehicleRepository.GetAllAsync();

        return vehicles.Select(vehicle => new VehicleResponse
        {
            Id = vehicle.Id,
            RegistrationNo = vehicle.RegistrationNo,
            Type = vehicle.Type,
            Capacity = vehicle.Capacity,
            Status = vehicle.Status,
            CreatedAt = vehicle.CreatedAt,
            UpdatedAt = vehicle.UpdatedAt
        }).ToList();
    }

    // US-07: Admin removes a vehicle from the registry

    public async Task DeleteVehicleAsync(Guid id)
    {
        var vehicle = await _vehicleRepository.GetByIdAsync(id);

        if (vehicle is null)
        {
            throw new NotFoundException(
                $"No vehicle found with id '{id}'.");
        }

        // SCRUM-85: check the vehicle's OWN status first, independent of the
        // Trip service. A vehicle that is currently On Trip must never be
        // removed. (Under Maintenance vehicles can still be deleted - that is
        // how a broken/retired vehicle leaves the registry.)
        if (vehicle.Status == VehicleStatus.OnTrip)
        {
            throw new InvalidOperationException(
                "Cannot remove a vehicle that is currently on a trip.");
        }

        var hasActiveOrUpcomingTrips =
            await _tripEligibilityChecker.HasActiveOrUpcomingTripsAsync(id);

        if (hasActiveOrUpcomingTrips)
        {
            throw new InvalidOperationException(
                "Cannot remove a vehicle with active trips.");
        }

        await _vehicleRepository.DeleteVehicleAsync(id);
    }
}