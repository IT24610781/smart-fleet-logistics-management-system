using TrackingTelemetryService.Data;
using TrackingTelemetryService.Models;
using TrackingTelemetryService.Services;

namespace TrackingTelemetryService.Tests;

/// <summary>
/// SCRUM-128/130 (US-15): TrackingService.GetFleetLatestAsync, tested without
/// MySQL or a live Trip API. Agreed rules under test: scope is In-Progress
/// trips only, a ping is "fresh" at up to 5 minutes old and "stale" beyond
/// that, and a trip with no ping at all is "unavailable".
/// </summary>
public class FleetLocationTests
{
    private static readonly Guid TripId = Guid.NewGuid();
    private static readonly Guid VehicleId = Guid.NewGuid();
    private static readonly Guid DriverId = Guid.NewGuid();

    [Fact]
    public async Task GetFleetLatest_NoInProgressTrips_ReturnsEmptyListWithoutQueryingRepository()
    {
        var repository = new InMemoryTrackingRepository();
        var tripClient = new FakeTripDispatchClient { InProgressTrips = [] };
        var service = new TrackingService(repository, tripClient);

        var result = await service.GetFleetLatestAsync("Bearer x", CancellationToken.None);

        Assert.Empty(result);
        // An empty fleet is a free short-circuit - no point joining against
        // an empty id list.
        Assert.Null(repository.LastRequestedTripIds);
    }

    [Fact]
    public async Task GetFleetLatest_PingWithinFiveMinutes_ClassifiedAsFresh()
    {
        var repository = new InMemoryTrackingRepository();
        repository.SeedLatest(TripId, DriverId, VehicleId, DateTime.UtcNow.AddMinutes(-1));
        var tripClient = new FakeTripDispatchClient
        {
            InProgressTrips = [Snapshot(TripId)]
        };
        var service = new TrackingService(repository, tripClient);

        var result = await service.GetFleetLatestAsync("Bearer x", CancellationToken.None);

        var entry = Assert.Single(result);
        Assert.Equal("fresh", entry.Freshness);
        Assert.NotNull(entry.Latitude);
        Assert.NotNull(entry.Longitude);
        Assert.NotNull(entry.ObservedAt);
    }

    [Fact]
    public async Task GetFleetLatest_PingOlderThanFiveMinutes_ClassifiedAsStale()
    {
        var repository = new InMemoryTrackingRepository();
        repository.SeedLatest(TripId, DriverId, VehicleId, DateTime.UtcNow.AddMinutes(-6));
        var tripClient = new FakeTripDispatchClient { InProgressTrips = [Snapshot(TripId)] };
        var service = new TrackingService(repository, tripClient);

        var result = await service.GetFleetLatestAsync("Bearer x", CancellationToken.None);

        Assert.Equal("stale", Assert.Single(result).Freshness);
    }

    [Fact]
    public async Task GetFleetLatest_NoPingEver_ClassifiedAsUnavailableWithNullCoordinates()
    {
        var repository = new InMemoryTrackingRepository(); // nothing seeded
        var tripClient = new FakeTripDispatchClient { InProgressTrips = [Snapshot(TripId)] };
        var service = new TrackingService(repository, tripClient);

        var result = await service.GetFleetLatestAsync("Bearer x", CancellationToken.None);

        var entry = Assert.Single(result);
        Assert.Equal("unavailable", entry.Freshness);
        Assert.Null(entry.Latitude);
        Assert.Null(entry.Longitude);
        Assert.Null(entry.ObservedAt);
    }

    [Fact]
    public async Task GetFleetLatest_PingJustInsideFiveMinutes_StillFresh()
    {
        var repository = new InMemoryTrackingRepository();
        repository.SeedLatest(TripId, DriverId, VehicleId, DateTime.UtcNow.AddMinutes(-4).AddSeconds(-55));
        var tripClient = new FakeTripDispatchClient { InProgressTrips = [Snapshot(TripId)] };
        var service = new TrackingService(repository, tripClient);

        var result = await service.GetFleetLatestAsync("Bearer x", CancellationToken.None);

        Assert.Equal("fresh", Assert.Single(result).Freshness);
    }

    [Fact]
    public async Task GetFleetLatest_JustOverFiveMinuteBoundary_IsStale()
    {
        var repository = new InMemoryTrackingRepository();
        repository.SeedLatest(TripId, DriverId, VehicleId, DateTime.UtcNow.AddMinutes(-5).AddSeconds(-1));
        var tripClient = new FakeTripDispatchClient { InProgressTrips = [Snapshot(TripId)] };
        var service = new TrackingService(repository, tripClient);

        var result = await service.GetFleetLatestAsync("Bearer x", CancellationToken.None);

        Assert.Equal("stale", Assert.Single(result).Freshness);
    }

    [Fact]
    public async Task GetFleetLatest_MixOfFreshStaleAndUnavailable_ClassifiesEachIndependently()
    {
        var freshTrip = Guid.NewGuid();
        var staleTrip = Guid.NewGuid();
        var unavailableTrip = Guid.NewGuid();

        var repository = new InMemoryTrackingRepository();
        repository.SeedLatest(freshTrip, DriverId, VehicleId, DateTime.UtcNow.AddSeconds(-30));
        repository.SeedLatest(staleTrip, DriverId, VehicleId, DateTime.UtcNow.AddMinutes(-20));
        // unavailableTrip intentionally has no seeded ping at all.

        var tripClient = new FakeTripDispatchClient
        {
            InProgressTrips = [Snapshot(freshTrip), Snapshot(staleTrip), Snapshot(unavailableTrip)]
        };
        var service = new TrackingService(repository, tripClient);

        var result = await service.GetFleetLatestAsync("Bearer x", CancellationToken.None);

        Assert.Equal(3, result.Count);
        Assert.Equal("fresh", result.Single(r => r.TripId == freshTrip).Freshness);
        Assert.Equal("stale", result.Single(r => r.TripId == staleTrip).Freshness);
        Assert.Equal("unavailable", result.Single(r => r.TripId == unavailableTrip).Freshness);
    }

    [Fact]
    public async Task GetFleetLatest_ScopesRepositoryQueryToExactlyTheInProgressTripIds()
    {
        var otherInProgressTrip = Guid.NewGuid();
        var repository = new InMemoryTrackingRepository();
        var tripClient = new FakeTripDispatchClient
        {
            InProgressTrips = [Snapshot(TripId), Snapshot(otherInProgressTrip)]
        };
        var service = new TrackingService(repository, tripClient);

        await service.GetFleetLatestAsync("Bearer x", CancellationToken.None);

        Assert.NotNull(repository.LastRequestedTripIds);
        Assert.Equal(
            new[] { TripId, otherInProgressTrip }.OrderBy(x => x),
            repository.LastRequestedTripIds!.OrderBy(x => x));
    }

    [Fact]
    public async Task GetFleetLatest_CopiesTripFieldsFromSnapshotVerbatim()
    {
        var repository = new InMemoryTrackingRepository();
        var snapshot = Snapshot(TripId, pickup: "Colombo Fort", dropoff: "Kandy");
        var tripClient = new FakeTripDispatchClient { InProgressTrips = [snapshot] };
        var service = new TrackingService(repository, tripClient);

        var entry = Assert.Single(await service.GetFleetLatestAsync("Bearer x", CancellationToken.None));

        Assert.Equal(TripId, entry.TripId);
        Assert.Equal(VehicleId, entry.VehicleId);
        Assert.Equal(DriverId, entry.DriverId);
        Assert.Equal("Colombo Fort", entry.PickupLocation);
        Assert.Equal("Kandy", entry.DropoffLocation);
    }

    [Fact]
    public async Task GetFleetLatest_TripServiceUnreachable_PropagatesFailure()
    {
        // AC 4: a tracking-service/Trip-service failure must surface as a
        // clear error rather than a silently empty or partial fleet list.
        var repository = new InMemoryTrackingRepository();
        var tripClient = new FakeTripDispatchClient
        {
            Exception = new TripServiceUnavailableException(
                "Fleet location data could not be retrieved.", new HttpRequestException())
        };
        var service = new TrackingService(repository, tripClient);

        await Assert.ThrowsAsync<TripServiceUnavailableException>(
            () => service.GetFleetLatestAsync("Bearer x", CancellationToken.None));
    }

    private static FleetTripSnapshot Snapshot(Guid tripId, string pickup = "Pickup", string dropoff = "Dropoff") =>
        new(tripId, "In-Progress", VehicleId, DriverId, pickup, dropoff);

    private sealed class InMemoryTrackingRepository : ITrackingRepository
    {
        private readonly Dictionary<Guid, TripLocationUpdate> _latestByTrip = new();
        public List<Guid>? LastRequestedTripIds { get; private set; }

        public void SeedLatest(Guid tripId, Guid driverId, Guid vehicleId, DateTime observedAt) =>
            _latestByTrip[tripId] = new TripLocationUpdate
            {
                Id = Guid.NewGuid(), TripId = tripId, DriverId = driverId, VehicleId = vehicleId,
                Latitude = 7m, Longitude = 80m, ObservedAt = observedAt, ReceivedAt = observedAt
            };

        public Task CreateAsync(TripLocationUpdate update) =>
            throw new NotSupportedException("Not exercised by fleet-latest tests.");

        public Task<List<TripLocationUpdate>> GetByTripIdAsync(Guid tripId, int limit) =>
            throw new NotSupportedException("Not exercised by fleet-latest tests.");

        public Task<Dictionary<Guid, TripLocationUpdate>> GetLatestPerTripAsync(List<Guid> tripIds)
        {
            LastRequestedTripIds = tripIds;
            var result = new Dictionary<Guid, TripLocationUpdate>();
            foreach (var id in tripIds)
            {
                if (_latestByTrip.TryGetValue(id, out var location))
                {
                    result[id] = location;
                }
            }
            return Task.FromResult(result);
        }
    }

    private sealed class FakeTripDispatchClient : ITripDispatchClient
    {
        public List<FleetTripSnapshot> InProgressTrips { get; set; } = [];
        public Exception? Exception { get; set; }

        public Task<TripSnapshot?> GetTripAsync(Guid tripId, string? authorizationHeader, CancellationToken cancellationToken) =>
            throw new NotSupportedException("Not exercised by fleet-latest tests.");

        public Task<List<FleetTripSnapshot>> GetInProgressTripsAsync(string? authorizationHeader, CancellationToken cancellationToken)
        {
            if (Exception is not null) throw Exception;
            return Task.FromResult(InProgressTrips);
        }
        
        public Task<List<CompletedTripSnapshot>> GetCompletedTripsAsync(string? authorizationHeader, CancellationToken cancellationToken) =>
            throw new NotSupportedException("Not exercised by FleetLocationTests.");
    }
}