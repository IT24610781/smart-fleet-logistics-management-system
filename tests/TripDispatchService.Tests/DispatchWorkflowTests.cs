using TripDispatchService.Data;
using TripDispatchService.DTOs;
using TripDispatchService.Models;
using TripDispatchService.Services;

namespace TripDispatchService.Tests;

/// <summary>US09 to US12 service logic tested without MySQL or a live Fleet API.</summary>
[Trait("Category", "Unit")]
public class DispatchWorkflowTests
{
    [Theory]
    [InlineData("pickup")]
    [InlineData("dropoff")]
    [InlineData("capacity")]
    [InlineData("negative")]
    [InlineData("coordinates")]
    public async Task CreateTrip_InvalidInput_DoesNotPersist(string field)
    {
        var repository = new InMemoryTripRepository();
        var service = new TripService(repository, new FakeFleetClient());
        var request = new CreateTripRequest
        {
            PickupLocation = "QA pickup",
            DropoffLocation = "QA destination",
            RequiredCapacity = "6 persons"
        };
        switch (field)
        {
            case "pickup": request.PickupLocation = ""; break;
            case "dropoff": request.DropoffLocation = ""; break;
            case "capacity": request.RequiredCapacity = ""; break;
            case "negative": request.RequiredCapacity = "-1 persons"; break;
            case "coordinates": request.PickupLat = 91; break;
        }

        await Assert.ThrowsAsync<ValidationException>(() => service.CreateTripAsync(request, Guid.NewGuid()));

        Assert.Equal(0, repository.Writes);
        Assert.Null(repository.Trip);
    }

    [Fact]
    public async Task SuggestVehicles_NoMatch_DoesNotMutateTrips()
    {
        var repository = new InMemoryTripRepository();
        var service = new TripService(repository, new FakeFleetClient());

        Assert.Empty(await service.GetSuggestionsForCapacityAsync("6 persons", null));
        Assert.Equal(0, repository.Writes);
    }

    [Fact]
    public async Task SuggestVehicles_FiltersCapacityUnitsAndAvailability_ThenRanksExcessCapacity()
    {
        var fleet = new FakeFleetClient
        {
            Vehicles =
            [
                new() { Id = Guid.NewGuid(), Capacity = "3 persons", Status = "Available" },
                new() { Id = Guid.NewGuid(), Capacity = "8 persons", Status = "On Trip" },
                new() { Id = Guid.NewGuid(), Capacity = "8 kg", Status = "Available" },
                new() { Id = Guid.NewGuid(), Capacity = "10 persons", Status = "Available" },
                new() { Id = Guid.NewGuid(), Capacity = "6 persons", Status = "Available" }
            ]
        };
        var service = new TripService(new InMemoryTripRepository(), fleet);

        var matches = await service.GetSuggestionsForCapacityAsync("6 persons", null);

        Assert.Equal(2, matches.Count);
        Assert.Equal("6 persons", matches[0].Capacity);
        Assert.Equal("10 persons", matches[1].Capacity);
    }

    [Fact]
    public async Task AssignVehicle_FleetRejects_DoesNotPartiallyAssignTrip()
    {
        var trip = new Trip { Id = Guid.NewGuid(), Status = TripStatus.Requested };
        var repository = new InMemoryTripRepository { Trip = trip };
        var service = new TripService(repository, new FakeFleetClient { RejectStatusUpdate = true });

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.AssignVehicleAsync(trip.Id, Guid.NewGuid(), Guid.NewGuid(), null));

        Assert.Equal(TripStatus.Requested, trip.Status);
        Assert.Null(trip.AssignedVehicleId);
        Assert.Null(trip.AssignedDriverId);
        Assert.Equal(0, repository.Writes);
    }

    [Fact]
    public async Task AcceptTrip_AlreadyInProgress_DoesNotChangeStateOrAudit()
    {
        var driverId = Guid.NewGuid();
        var trip = new Trip
        {
            Id = Guid.NewGuid(), AssignedDriverId = driverId,
            Status = TripStatus.InProgress, UpdatedAt = DateTime.UtcNow
        };
        var timestamp = trip.UpdatedAt;
        var repository = new InMemoryTripRepository { Trip = trip };
        var service = new TripService(repository, new FakeFleetClient());

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.RespondAsync(trip.Id, driverId, "accept", null));

        Assert.Equal(TripStatus.InProgress, trip.Status);
        Assert.Equal(timestamp, trip.UpdatedAt);
        Assert.Equal(0, repository.Writes);
    }

    [Theory]
    [InlineData("6 persons", "Available", true)]
    [InlineData("5.99 persons", "Available", false)]
    [InlineData("6 PERSONS", "available", true)]
    [InlineData("6 kg", "Available", false)]
    [InlineData("8 persons", "On Trip", false)]
    [InlineData("8 persons", "Under Maintenance", false)]
    [InlineData("not-a-capacity", "Available", false)]
    public async Task SuggestVehicles_CandidateRules_UseControlledFleetResponse(
        string capacity, string status, bool expectedMatch)
    {
        var candidate = new FleetVehicleDto { Id = Guid.NewGuid(), Capacity = capacity, Status = status };
        var fleet = new FakeFleetClient { Vehicles = [candidate] };
        var repository = new InMemoryTripRepository();
        var service = new TripService(repository, fleet);

        var result = await service.GetSuggestionsForCapacityAsync("6 persons", "Bearer unit-dispatcher");

        Assert.Equal(expectedMatch ? 1 : 0, result.Count);
        if (expectedMatch) Assert.Equal(candidate.Id, Assert.Single(result).VehicleId);
        Assert.Equal(1, fleet.Reads);
        Assert.Equal("Bearer unit-dispatcher", fleet.LastAuthorization);
        Assert.Equal(0, repository.Writes);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("0 persons")]
    [InlineData("-1 persons")]
    [InlineData("invalid")]
    public async Task SuggestVehicles_InvalidCapacity_DoesNotContactFleet(string? capacity)
    {
        var fleet = new FakeFleetClient();
        var repository = new InMemoryTripRepository();
        var service = new TripService(repository, fleet);

        var error = await Assert.ThrowsAsync<ValidationException>(() =>
            service.GetSuggestionsForCapacityAsync(capacity, "Bearer unit-dispatcher"));

        Assert.Contains("requiredCapacity", error.Errors.Keys);
        Assert.Equal(0, fleet.Reads);
        Assert.Equal(0, repository.Writes);
    }

    [Fact]
    public async Task SuggestVehicles_FleetUnavailable_PropagatesFailureWithoutWriting()
    {
        var failure = new FleetVehicleUnavailableException("Simulated Fleet failure", new HttpRequestException());
        var fleet = new FakeFleetClient { ReadFailure = failure };
        var repository = new InMemoryTripRepository();
        var service = new TripService(repository, fleet);

        var actual = await Assert.ThrowsAsync<FleetVehicleUnavailableException>(() =>
            service.GetSuggestionsForCapacityAsync("6 persons", "Bearer unit-dispatcher"));

        Assert.Same(failure, actual);
        Assert.Equal(1, fleet.Reads);
        Assert.Equal(0, repository.Writes);
    }

    private sealed class InMemoryTripRepository : ITripRepository
    {
        public Trip? Trip { get; set; }
        public int Writes { get; private set; }

        public Task CreateTripAsync(Trip trip)
        {
            Trip = trip;
            Writes++;
            return Task.CompletedTask;
        }

        public Task UpdateAsync(Trip trip)
        {
            Trip = trip;
            Writes++;
            return Task.CompletedTask;
        }

        public Task<Trip?> GetByIdAsync(Guid id) => Task.FromResult(Trip?.Id == id ? Trip : null);
        public Task<List<Trip>> GetAllAsync(string? status) =>
            Task.FromResult(Trip is null ? new List<Trip>() : new List<Trip> { Trip });
        public Task<List<Trip>> GetByDriverIdAsync(Guid id) => Task.FromResult(new List<Trip>());
        public Task<List<Trip>> GetByCreatedDateRangeAsync(DateTime start, DateTime end) =>
            throw new NotSupportedException("Not exercised by DispatchWorkflowTests.");
    }

    private sealed class FakeFleetClient : IFleetVehicleClient
    {
        public List<FleetVehicleDto> Vehicles { get; init; } = [];
        public bool RejectStatusUpdate { get; init; }
        public Exception? ReadFailure { get; init; }
        public int Reads { get; private set; }
        public string? LastAuthorization { get; private set; }

        public Task<List<FleetVehicleDto>> GetVehiclesAsync(string? authorizationHeader)
        {
            Reads++;
            LastAuthorization = authorizationHeader;
            if (ReadFailure is not null) throw ReadFailure;
            return Task.FromResult(Vehicles);
        }

        public Task SetVehicleStatusAsync(Guid id, string status, string? authorizationHeader)
        {
            if (RejectStatusUpdate) throw new InvalidOperationException("Vehicle unavailable");
            return Task.CompletedTask;
        }
    }
}
