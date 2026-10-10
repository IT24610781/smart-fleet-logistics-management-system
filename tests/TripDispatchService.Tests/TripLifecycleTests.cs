using TripDispatchService.Data;
using TripDispatchService.DTOs;
using TripDispatchService.Models;
using TripDispatchService.Services;

namespace TripDispatchService.Tests;

public class TripLifecycleTests
{
    [Fact]
    public async Task Reject_Invalid_Lifecycle_Transition()
    {
        // Arrange
        var tripId = Guid.NewGuid();
        var driverId = Guid.NewGuid();

        var trip = new Trip
        {
            Id = tripId,
            PickupLocation = "Test Pickup",
            DropoffLocation = "Test Dropoff",
            RequiredCapacity = "6 persons",
            Status = TripStatus.Requested,
            AssignedDriverId = driverId,
            AssignedVehicleId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var repository = new FakeTripRepository(trip);
        var fleetClient = new FakeFleetVehicleClient();
        var service = new TripService(repository, fleetClient);

        // Act
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.RespondAsync(
                tripId,
                driverId,
                "accept",
                null));

        // Assert
        Assert.Contains("expected 'Assigned'", exception.Message);
        Assert.Equal(TripStatus.Requested, trip.Status);
        Assert.False(repository.UpdateCalled);
    }

    [Fact]
    public async Task Status_Update_Preserves_Required_Audit_Fields()
    {
        // Arrange
        var tripId = Guid.NewGuid();
        var driverId = Guid.NewGuid();

        var createdAt = DateTime.UtcNow.AddMinutes(-10);
        var originalUpdatedAt = createdAt;

        var trip = new Trip
        {
            Id = tripId,
            PickupLocation = "Audit Test Pickup",
            DropoffLocation = "Audit Test Dropoff",
            RequiredCapacity = "6 persons",
            Status = TripStatus.Assigned,
            AssignedDriverId = driverId,
            AssignedVehicleId = Guid.NewGuid(),
            CreatedAt = createdAt,
            UpdatedAt = originalUpdatedAt
        };

        var repository = new FakeTripRepository(trip);
        var fleetClient = new FakeFleetVehicleClient();
        var service = new TripService(repository, fleetClient);

        // Act
        var response = await service.RespondAsync(
            tripId,
            driverId,
            "accept",
            null);

        // Assert
        Assert.Equal(TripStatus.InProgress, response.Status);

        // CreatedAt must remain unchanged.
        Assert.Equal(createdAt, trip.CreatedAt);
        Assert.Equal(createdAt, response.CreatedAt);

        // UpdatedAt must be refreshed.
        Assert.True(trip.UpdatedAt > originalUpdatedAt);
        Assert.Equal(trip.UpdatedAt, response.UpdatedAt);

        // Persistence must be requested.
        Assert.True(repository.UpdateCalled);
    }

    private sealed class FakeTripRepository : ITripRepository
    {
        private readonly Trip _trip;

        public bool UpdateCalled { get; private set; }

        public FakeTripRepository(Trip trip)
        {
            _trip = trip;
        }

        public Task CreateTripAsync(Trip trip)
        {
            return Task.CompletedTask;
        }

        public Task<List<Trip>> GetAllAsync(string? status)
        {
            return Task.FromResult(new List<Trip> { _trip });
        }

        public Task<Trip?> GetByIdAsync(Guid id)
        {
            return Task.FromResult<Trip?>(
                id == _trip.Id ? _trip : null);
        }

        public Task UpdateAsync(Trip trip)
        {
            UpdateCalled = true;
            return Task.CompletedTask;
        }

        public Task<List<Trip>> GetByDriverIdAsync(Guid driverId)
        {
            return Task.FromResult(
                driverId == _trip.AssignedDriverId
                    ? new List<Trip> { _trip }
                    : new List<Trip>());
        }

        public Task<List<Trip>> GetByCreatedDateRangeAsync(DateTime start, DateTime end) =>
            throw new NotSupportedException("Not exercised by TripLifecycleTests.");
    }

    private sealed class FakeFleetVehicleClient : IFleetVehicleClient
    {
        public Task<List<FleetVehicleDto>> GetVehiclesAsync(
            string? authorizationHeader)
        {
            return Task.FromResult(new List<FleetVehicleDto>());
        }

        public Task SetVehicleStatusAsync(
            Guid vehicleId,
            string status,
            string? authorizationHeader)
        {
            return Task.CompletedTask;
        }
    }
}
