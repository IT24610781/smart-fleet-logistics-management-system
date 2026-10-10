using TripDispatchService.Data;
using TripDispatchService.DTOs;
using TripDispatchService.Models;
using TripDispatchService.Services;

namespace TripDispatchService.Tests;

// Driver-facing "mark as Completed" action, added alongside the auto
// location/busy-driver features so the chat frontend can offer a Driver a
// one-tap way to close out an In-Progress trip.
public class DriverCompleteTripTests
{
    [Fact]
    public async Task InProgressTrip_OwnedByCaller_BecomesCompleted_AndFreesVehicle()
    {
        var tripId = Guid.NewGuid();
        var driverId = Guid.NewGuid();
        var vehicleId = Guid.NewGuid();

        var trip = new Trip
        {
            Id = tripId,
            PickupLocation = "Pickup",
            DropoffLocation = "Dropoff",
            RequiredCapacity = "6 persons",
            Status = TripStatus.InProgress,
            AssignedDriverId = driverId,
            AssignedVehicleId = vehicleId,
            CreatedAt = DateTime.UtcNow.AddHours(-1),
            UpdatedAt = DateTime.UtcNow.AddHours(-1)
        };

        var repository = new FakeTripRepository(trip);
        var fleetClient = new FakeFleetVehicleClient();
        var service = new TripService(repository, fleetClient);

        var response = await service.CompleteTripAsync(tripId, driverId, null);

        Assert.Equal(TripStatus.Completed, response.Status);
        Assert.Equal(TripStatus.Completed, trip.Status);
        Assert.True(repository.UpdateCalled);
        Assert.Equal((vehicleId, "Available"), fleetClient.LastStatusSet);
    }

    [Fact]
    public async Task TripNotAssignedToCaller_ThrowsNotFound_NotForbidden()
    {
        var tripId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var otherDriverId = Guid.NewGuid();

        var trip = new Trip
        {
            Id = tripId,
            PickupLocation = "Pickup",
            DropoffLocation = "Dropoff",
            RequiredCapacity = "6 persons",
            Status = TripStatus.InProgress,
            AssignedDriverId = ownerId,
            AssignedVehicleId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var repository = new FakeTripRepository(trip);
        var service = new TripService(repository, new FakeFleetVehicleClient());

        // Same privacy pattern as RespondAsync: a Driver can't distinguish
        // "not yours" from "doesn't exist".
        await Assert.ThrowsAsync<NotFoundException>(
            () => service.CompleteTripAsync(tripId, otherDriverId, null));
        Assert.False(repository.UpdateCalled);
    }

    [Theory]
    [InlineData(TripStatus.Requested)]
    [InlineData(TripStatus.Assigned)]
    [InlineData(TripStatus.Completed)]
    [InlineData(TripStatus.Cancelled)]
    public async Task TripNotInProgress_ThrowsConflict(string currentStatus)
    {
        var tripId = Guid.NewGuid();
        var driverId = Guid.NewGuid();

        var trip = new Trip
        {
            Id = tripId,
            PickupLocation = "Pickup",
            DropoffLocation = "Dropoff",
            RequiredCapacity = "6 persons",
            Status = currentStatus,
            AssignedDriverId = driverId,
            AssignedVehicleId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var repository = new FakeTripRepository(trip);
        var service = new TripService(repository, new FakeFleetVehicleClient());

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.CompleteTripAsync(tripId, driverId, null));

        Assert.Contains("expected 'In-Progress'", exception.Message);
        Assert.Equal(currentStatus, trip.Status);
        Assert.False(repository.UpdateCalled);
    }

    private sealed class FakeTripRepository : ITripRepository
    {
        private readonly Trip _trip;

        public bool UpdateCalled { get; private set; }

        public FakeTripRepository(Trip trip)
        {
            _trip = trip;
        }

        public Task CreateTripAsync(Trip trip) => Task.CompletedTask;

        public Task<List<Trip>> GetAllAsync(string? status) =>
            Task.FromResult(new List<Trip> { _trip });

        public Task<Trip?> GetByIdAsync(Guid id) =>
            Task.FromResult<Trip?>(id == _trip.Id ? _trip : null);

        public Task UpdateAsync(Trip trip)
        {
            UpdateCalled = true;
            return Task.CompletedTask;
        }

        public Task<List<Trip>> GetByDriverIdAsync(Guid driverId) =>
            Task.FromResult(
                driverId == _trip.AssignedDriverId
                    ? new List<Trip> { _trip }
                    : new List<Trip>());

        public Task<List<Trip>> GetByCreatedDateRangeAsync(DateTime start, DateTime end) =>
            throw new NotSupportedException("Not exercised by DriverCompleteTripTests.");
    }

    private sealed class FakeFleetVehicleClient : IFleetVehicleClient
    {
        public (Guid VehicleId, string Status)? LastStatusSet { get; private set; }

        public Task<List<FleetVehicleDto>> GetVehiclesAsync(string? authorizationHeader) =>
            Task.FromResult(new List<FleetVehicleDto>());

        public Task SetVehicleStatusAsync(Guid vehicleId, string status, string? authorizationHeader)
        {
            LastStatusSet = (vehicleId, status);
            return Task.CompletedTask;
        }
    }
}