using FleetVehicleService.Data;
using FleetVehicleService.DTOs;
using FleetVehicleService.Models;

namespace FleetVehicleService.Services;

public class VehicleService
{
    private readonly VehicleRepository _vehicleRepository;
    private readonly IVehicleEventPublisher _eventPublisher;
    private readonly ITripEligibilityChecker _tripEligibilityChecker;

    public VehicleService(
        VehicleRepository vehicleRepository,
        IVehicleEventPublisher eventPublisher,
        ITripEligibilityChecker tripEligibilityChecker)
    {
        _vehicleRepository = vehicleRepository;
        _eventPublisher = eventPublisher;
        _tripEligibilityChecker = tripEligibilityChecker;
    }

    // ============================================================
    // US-04: Admin registers a vehicle
    // ============================================================

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

    if (string.IsNullOrWhiteSpace(type))
    {
        errors["type"] = "Vehicle type is required.";
    }

    var capacity = request.Capacity?.Trim() ?? string.Empty;

    if (string.IsNullOrWhiteSpace(capacity))
    {
        errors["capacity"] = "Capacity is required.";
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


    // ============================================================
    // US-06: Admin updates a vehicle's details and status
    // ============================================================

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

    var type = request.Type?.Trim() ?? string.Empty;

    if (string.IsNullOrWhiteSpace(type))
    {
        errors["type"] = "Vehicle type is required.";
    }

    var capacity = request.Capacity?.Trim() ?? string.Empty;

    if (string.IsNullOrWhiteSpace(capacity))
    {
        errors["capacity"] = "Capacity is required.";
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
    
    
    // ============================================================
    // US-05: Dispatcher/Admin views all registered vehicles
    // ============================================================

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


    // ============================================================
    // US-07: Admin removes a vehicle from the registry
    // ============================================================

    public async Task DeleteVehicleAsync(Guid id)
    {
        var vehicle = await _vehicleRepository.GetByIdAsync(id);

        if (vehicle is null)
        {
            throw new NotFoundException(
                $"No vehicle found with id '{id}'.");
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