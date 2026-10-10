using TrackingTelemetryService.Data;
using TrackingTelemetryService.DTOs;
using TrackingTelemetryService.Models;
using TrackingTelemetryService.Services;

namespace TrackingTelemetryService.Tests;

/// <summary>SCRUM-23 (US-14) service logic tested without MySQL or a live Trip API.</summary>
public class TrackingWorkflowTests
{
    private static readonly Guid DriverId = Guid.NewGuid();
    private static readonly Guid OtherDriverId = Guid.NewGuid();
    private static readonly Guid TripId = Guid.NewGuid();
    private static readonly Guid VehicleId = Guid.NewGuid();

    [Theory]
    [InlineData("tripId")]
    [InlineData("latitude")]
    [InlineData("longitude")]
    [InlineData("observedAt")]
    [InlineData("futureTimestamp")]
    [InlineData("staleTimestamp")]
    public async Task SubmitLocation_InvalidInput_DoesNotPersistOrCallTripService(string field)
    {
        var repository = new InMemoryTrackingRepository();
        var tripClient = new FakeTripDispatchClient { Result = new TripSnapshot(TripId, "In-Progress", DriverId, VehicleId) };
        var service = new TrackingService(repository, tripClient);
        var request = ValidRequest();

        switch (field)
        {
            case "tripId": request.TripId = null; break;
            case "latitude": request.Latitude = 200m; break;
            case "longitude": request.Longitude = -200m; break;
            case "observedAt": request.ObservedAt = null; break;
            case "futureTimestamp": request.ObservedAt = DateTime.UtcNow.AddMinutes(10); break;
            case "staleTimestamp": request.ObservedAt = DateTime.UtcNow.AddHours(-48); break;
        }

        await Assert.ThrowsAsync<ValidationException>(
            () => service.SubmitLocationAsync(DriverId, request, "Bearer x", CancellationToken.None));

        Assert.Equal(0, repository.Writes);
        // Invalid input is rejected before Trip is ever contacted.
        Assert.Equal(0, tripClient.CallCount);
    }

    [Fact]
    public async Task SubmitLocation_SmallClockSkewTolerated_PersistsWithCorrectAssociations()
    {
        var repository = new InMemoryTrackingRepository();
        var tripClient = new FakeTripDispatchClient { Result = new TripSnapshot(TripId, "In-Progress", DriverId, VehicleId) };
        var service = new TrackingService(repository, tripClient);

        var result = await service.SubmitLocationAsync(DriverId, new SubmitLocationRequest
        {
            TripId = TripId, Latitude = 7.2513m, Longitude = 80.3464m, ObservedAt = DateTime.UtcNow.AddMinutes(1)
        }, "Bearer x", CancellationToken.None);

        Assert.Equal(1, repository.Writes);
        Assert.Equal(TripId, repository.LastWrite!.TripId);
        Assert.Equal(DriverId, repository.LastWrite!.DriverId);
        Assert.Equal(VehicleId, repository.LastWrite!.VehicleId);
        Assert.Equal(7.2513m, result.Latitude);
    }

    [Fact]
    public async Task SubmitLocation_AnotherDriversTrip_LooksIdenticalToNotFound()
    {
        var repository = new InMemoryTrackingRepository();
        var tripClient = new FakeTripDispatchClient { Result = new TripSnapshot(TripId, "In-Progress", OtherDriverId, VehicleId) };
        var service = new TrackingService(repository, tripClient);

        var wrongDriverEx = await Assert.ThrowsAsync<NotFoundException>(
            () => service.SubmitLocationAsync(DriverId, ValidRequest(), "Bearer x", CancellationToken.None));

        tripClient.Result = null; // genuinely nonexistent
        var noTripEx = await Assert.ThrowsAsync<NotFoundException>(
            () => service.SubmitLocationAsync(DriverId, ValidRequest(), "Bearer x", CancellationToken.None));

        Assert.Equal(noTripEx.Message, wrongDriverEx.Message);
        Assert.Equal(0, repository.Writes);
    }

    [Fact]
    public async Task SubmitLocation_TripNotInProgress_RejectsWithoutPersisting()
    {
        var repository = new InMemoryTrackingRepository();
        var tripClient = new FakeTripDispatchClient { Result = new TripSnapshot(TripId, "Assigned", DriverId, VehicleId) };
        var service = new TrackingService(repository, tripClient);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.SubmitLocationAsync(DriverId, ValidRequest(), "Bearer x", CancellationToken.None));

        Assert.Contains("Assigned", ex.Message);
        Assert.Equal(0, repository.Writes);
    }

    [Fact]
    public async Task SubmitLocation_DuplicateObservation_StoresBothWithoutMerging()
    {
        // Confirmed rule: raw telemetry is never silently dropped - two
        // submissions for the same trip and timestamp are both kept as
        // separate rows. Any de-duplication is a display-layer concern.
        var repository = new InMemoryTrackingRepository();
        var tripClient = new FakeTripDispatchClient { Result = new TripSnapshot(TripId, "In-Progress", DriverId, VehicleId) };
        var service = new TrackingService(repository, tripClient);
        var request = ValidRequest();

        await service.SubmitLocationAsync(DriverId, request, "Bearer x", CancellationToken.None);
        await service.SubmitLocationAsync(DriverId, request, "Bearer x", CancellationToken.None);

        Assert.Equal(2, repository.Writes);
        Assert.Equal(2, repository.Rows.Count(r => r.TripId == TripId));
    }

    [Fact]
    public async Task SubmitLocation_TripServiceUnreachable_PropagatesFailureWithoutPersisting()
    {
        var repository = new InMemoryTrackingRepository();
        var tripClient = new FakeTripDispatchClient
        {
            Exception = new TripServiceUnavailableException(
                "Trip status could not be verified. No location update was recorded.", new HttpRequestException())
        };
        var service = new TrackingService(repository, tripClient);

        await Assert.ThrowsAsync<TripServiceUnavailableException>(
            () => service.SubmitLocationAsync(DriverId, ValidRequest(), "Bearer x", CancellationToken.None));

        Assert.Equal(0, repository.Writes);
    }

    [Fact]
    public async Task GetHistory_ReturnsOnlyThisTripsRecordsInChronologicalOrder()
    {
        var repository = new InMemoryTrackingRepository();
        var earlier = DateTime.UtcNow.AddMinutes(-10);
        var later = DateTime.UtcNow.AddMinutes(-5);
        repository.Seed(TripId, DriverId, VehicleId, later);
        repository.Seed(TripId, DriverId, VehicleId, earlier);
        repository.Seed(Guid.NewGuid(), OtherDriverId, VehicleId, DateTime.UtcNow); // a different trip entirely

        var tripClient = new FakeTripDispatchClient { Result = new TripSnapshot(TripId, "In-Progress", DriverId, VehicleId) };
        var service = new TrackingService(repository, tripClient);

        var history = await service.GetHistoryAsync(TripId, null, null, "Bearer x", CancellationToken.None);

        Assert.Equal(2, history.Count);
        Assert.All(history, h => Assert.Equal(TripId, h.TripId));
        Assert.True(history[0].ObservedAt < history[1].ObservedAt);
    }

    [Fact]
    public async Task GetHistory_ValidTripWithNoRecords_ReturnsEmptyListNotError()
    {
        var repository = new InMemoryTrackingRepository();
        var tripClient = new FakeTripDispatchClient { Result = new TripSnapshot(TripId, "Assigned", DriverId, VehicleId) };
        var service = new TrackingService(repository, tripClient);

        var history = await service.GetHistoryAsync(TripId, null, null, "Bearer x", CancellationToken.None);

        Assert.Empty(history);
    }

    [Fact]
    public async Task GetHistory_DriverRequestingAnotherDriversTrip_ThrowsNotFound()
    {
        var repository = new InMemoryTrackingRepository();
        repository.Seed(TripId, OtherDriverId, VehicleId, DateTime.UtcNow);
        var tripClient = new FakeTripDispatchClient { Result = new TripSnapshot(TripId, "In-Progress", OtherDriverId, VehicleId) };
        var service = new TrackingService(repository, tripClient);

        await Assert.ThrowsAsync<NotFoundException>(
            () => service.GetHistoryAsync(TripId, DriverId, null, "Bearer x", CancellationToken.None));
    }

    [Fact]
    public async Task GetHistory_DispatcherOrAdmin_CanViewAnyDriversTrip()
    {
        var repository = new InMemoryTrackingRepository();
        repository.Seed(TripId, OtherDriverId, VehicleId, DateTime.UtcNow);
        var tripClient = new FakeTripDispatchClient { Result = new TripSnapshot(TripId, "In-Progress", OtherDriverId, VehicleId) };
        var service = new TrackingService(repository, tripClient);

        var history = await service.GetHistoryAsync(TripId, restrictToDriverId: null, null, "Bearer x", CancellationToken.None);

        Assert.Single(history);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    [InlineData(5000)]
    public async Task GetHistory_LimitOutsideAllowedRange_ThrowsValidation(int limit)
    {
        var repository = new InMemoryTrackingRepository();
        var tripClient = new FakeTripDispatchClient { Result = new TripSnapshot(TripId, "In-Progress", DriverId, VehicleId) };
        var service = new TrackingService(repository, tripClient);

        var ex = await Assert.ThrowsAsync<ValidationException>(
            () => service.GetHistoryAsync(TripId, null, limit, "Bearer x", CancellationToken.None));

        Assert.Contains("limit", ex.Errors.Keys);
    }

    [Fact]
    public async Task GetHistory_NoLimitGiven_DefaultsTo500()
    {
        var repository = new InMemoryTrackingRepository();
        var tripClient = new FakeTripDispatchClient { Result = new TripSnapshot(TripId, "In-Progress", DriverId, VehicleId) };
        var service = new TrackingService(repository, tripClient);

        await service.GetHistoryAsync(TripId, null, null, "Bearer x", CancellationToken.None);

        Assert.Equal(500, repository.LastRequestedLimit);
    }

    [Fact]
    public async Task GetHistory_NonexistentTrip_ThrowsNotFound()
    {
        var repository = new InMemoryTrackingRepository();
        var tripClient = new FakeTripDispatchClient { Result = null };
        var service = new TrackingService(repository, tripClient);

        await Assert.ThrowsAsync<NotFoundException>(
            () => service.GetHistoryAsync(TripId, null, null, "Bearer x", CancellationToken.None));
    }

    private static SubmitLocationRequest ValidRequest() => new()
    {
        TripId = TripId, Latitude = 7.2513m, Longitude = 80.3464m, ObservedAt = DateTime.UtcNow
    };

    private sealed class InMemoryTrackingRepository : ITrackingRepository
    {
        public readonly List<TripLocationUpdate> Rows = new();
        public int Writes { get; private set; }
        public TripLocationUpdate? LastWrite { get; private set; }
        public int? LastRequestedLimit { get; private set; }

        public Task CreateAsync(TripLocationUpdate update)
        {
            Rows.Add(update);
            LastWrite = update;
            Writes++;
            return Task.CompletedTask;
        }

        public Task<List<TripLocationUpdate>> GetByTripIdAsync(Guid tripId, int limit)
        {
            LastRequestedLimit = limit;
            var result = Rows.Where(r => r.TripId == tripId).OrderBy(r => r.ObservedAt).Take(limit).ToList();
            return Task.FromResult(result);
        }

        // SCRUM-128: not exercised by these US-14/SCRUM-134 tests - see
        // FleetLocationTests.cs for coverage of this method.
        public Task<Dictionary<Guid, TripLocationUpdate>> GetLatestPerTripAsync(List<Guid> tripIds) =>
            throw new NotSupportedException("Not exercised by TrackingWorkflowTests.");

        public void Seed(Guid tripId, Guid driverId, Guid vehicleId, DateTime observedAt) =>
            Rows.Add(new TripLocationUpdate
            {
                Id = Guid.NewGuid(), TripId = tripId, DriverId = driverId, VehicleId = vehicleId,
                Latitude = 7m, Longitude = 80m, ObservedAt = observedAt, ReceivedAt = observedAt
            });
    }

    private sealed class FakeTripDispatchClient : ITripDispatchClient
    {
        public TripSnapshot? Result { get; set; }
        public Exception? Exception { get; set; }
        public int CallCount { get; private set; }

        public Task<TripSnapshot?> GetTripAsync(Guid tripId, string? authorizationHeader, CancellationToken cancellationToken)
        {
            CallCount++;
            if (Exception is not null) throw Exception;
            return Task.FromResult(Result);
        }

        public Task<List<FleetTripSnapshot>> GetInProgressTripsAsync(string? authorizationHeader, CancellationToken cancellationToken) =>
            throw new NotSupportedException("Not exercised by TrackingWorkflowTests.");
    }
}