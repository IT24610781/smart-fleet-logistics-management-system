using TrackingTelemetryService.Data;
using TrackingTelemetryService.Models;
using TrackingTelemetryService.Services;

namespace TrackingTelemetryService.Tests;

/// <summary>
/// SCRUM-138/140 (US-17): FleetUtilizationService.GetFleetUtilizationAsync,
/// tested without any real HTTP calls or database. Agreed rules under
/// test: utilization is TIME IN USE (a Completed trip's location-ping
/// span, read straight from ITrackingRepository), clipped to the
/// requested period, per vehicle and as a fleet-wide summary - never the
/// vehicle's current status.
/// </summary>
public class FleetUtilizationServiceTests
{
    private static readonly DateTime PeriodStart = new(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime PeriodEnd = new(2026, 10, 8, 0, 0, 0, DateTimeKind.Utc); // 168 hours
    private static readonly Guid VehicleId = Guid.NewGuid();
    private static readonly Guid DriverId = Guid.NewGuid();

    [Fact]
    public async Task GetFleetUtilization_NullStartOrEnd_ThrowsValidation()
    {
        var service = BuildService();

        var ex = await Assert.ThrowsAsync<ValidationException>(
            () => service.GetFleetUtilizationAsync(null, PeriodEnd, "Bearer x", CancellationToken.None));

        Assert.True(ex.Errors.ContainsKey("start"));
    }

    [Fact]
    public async Task GetFleetUtilization_EndBeforeStart_ThrowsValidation()
    {
        var service = BuildService();

        var ex = await Assert.ThrowsAsync<ValidationException>(
            () => service.GetFleetUtilizationAsync(PeriodEnd, PeriodStart, "Bearer x", CancellationToken.None));

        Assert.True(ex.Errors.ContainsKey("end"));
    }

    [Fact]
    public async Task GetFleetUtilization_EndEqualsStart_ThrowsValidation()
    {
        // A zero-length window isn't a meaningful reporting period, even
        // though the AC's literal wording only calls out "end before start".
        var service = BuildService();

        await Assert.ThrowsAsync<ValidationException>(
            () => service.GetFleetUtilizationAsync(PeriodStart, PeriodStart, "Bearer x", CancellationToken.None));
    }

    [Fact]
    public async Task GetFleetUtilization_TripFullyInsidePeriod_CalculatesUtilizationFromPingSpan()
    {
        var tripId = Guid.NewGuid();
        var fleet = new FakeFleetVehicleClient { Vehicles = [Vehicle(VehicleId)] };
        var trips = new FakeTripDispatchClient
        {
            CompletedTrips = [CompletedTrip(tripId, VehicleId, PeriodStart.AddDays(1), PeriodStart.AddDays(1).AddHours(5))]
        };
        var repository = new InMemoryTrackingRepository();
        // Exactly 24 hours of pings, well inside the 168-hour period.
        repository.Seed(tripId, PeriodStart.AddDays(1), PeriodStart.AddDays(2));
        var service = BuildService(fleet, trips, repository);

        var report = await service.GetFleetUtilizationAsync(PeriodStart, PeriodEnd, "Bearer x", CancellationToken.None);

        var entry = Assert.Single(report.Vehicles);
        Assert.Equal(24, entry.InUseHours, 2);
        Assert.Equal(Math.Round(24.0 / 168 * 100, 2), entry.UtilizationPercent, 2);
        Assert.False(entry.HasInsufficientData);
        Assert.Equal(1, entry.CompletedTripCount);
    }

    [Fact]
    public async Task GetFleetUtilization_PingSpanOverlapsPeriodBoundary_OnlyClippedPortionCounted()
    {
        // AC Scenario 2: usage overlaps the period - only the portion
        // inside [start, end] should be counted.
        var tripId = Guid.NewGuid();
        var fleet = new FakeFleetVehicleClient { Vehicles = [Vehicle(VehicleId)] };
        var trips = new FakeTripDispatchClient
        {
            CompletedTrips = [CompletedTrip(tripId, VehicleId, PeriodStart.AddHours(-10), PeriodStart.AddHours(10))]
        };
        var repository = new InMemoryTrackingRepository();
        // Ping span: 10 hours before periodStart to 10 hours after it -
        // only the 10 hours after periodStart should count.
        repository.Seed(tripId, PeriodStart.AddHours(-10), PeriodStart.AddHours(10));
        var service = BuildService(fleet, trips, repository);

        var report = await service.GetFleetUtilizationAsync(PeriodStart, PeriodEnd, "Bearer x", CancellationToken.None);

        Assert.Equal(10, Assert.Single(report.Vehicles).InUseHours, 2);
    }

    [Fact]
    public async Task GetFleetUtilization_TripWithFewerThanTwoPings_FlagsInsufficientDataWithoutCountingHours()
    {
        var tripId = Guid.NewGuid();
        var fleet = new FakeFleetVehicleClient { Vehicles = [Vehicle(VehicleId)] };
        var trips = new FakeTripDispatchClient
        {
            CompletedTrips = [CompletedTrip(tripId, VehicleId, PeriodStart.AddDays(1), PeriodStart.AddDays(1).AddHours(1))]
        };
        var repository = new InMemoryTrackingRepository();
        repository.Seed(tripId, PeriodStart.AddDays(1)); // a single ping - no measurable span
        var service = BuildService(fleet, trips, repository);

        var report = await service.GetFleetUtilizationAsync(PeriodStart, PeriodEnd, "Bearer x", CancellationToken.None);

        var entry = Assert.Single(report.Vehicles);
        Assert.Equal(0, entry.InUseHours);
        Assert.True(entry.HasInsufficientData);
        Assert.Equal(1, report.Summary.VehiclesWithInsufficientData);
    }

    [Fact]
    public async Task GetFleetUtilization_TripWithNoPingsAtAll_FlagsInsufficientData()
    {
        // AC Scenario 3 equivalent for a single trip: an empty history is
        // read, not invented.
        var tripId = Guid.NewGuid();
        var fleet = new FakeFleetVehicleClient { Vehicles = [Vehicle(VehicleId)] };
        var trips = new FakeTripDispatchClient
        {
            CompletedTrips = [CompletedTrip(tripId, VehicleId, PeriodStart.AddDays(1), PeriodStart.AddDays(1).AddHours(1))]
        };
        var repository = new InMemoryTrackingRepository(); // nothing seeded for tripId
        var service = BuildService(fleet, trips, repository);

        var report = await service.GetFleetUtilizationAsync(PeriodStart, PeriodEnd, "Bearer x", CancellationToken.None);

        Assert.True(Assert.Single(report.Vehicles).HasInsufficientData);
    }

    [Fact]
    public async Task GetFleetUtilization_VehicleWithNoCompletedTrips_ReportsRealZeroNotUnavailable()
    {
        var fleet = new FakeFleetVehicleClient { Vehicles = [Vehicle(VehicleId)] };
        var trips = new FakeTripDispatchClient { CompletedTrips = [] };
        var service = BuildService(fleet, trips, new InMemoryTrackingRepository());

        var report = await service.GetFleetUtilizationAsync(PeriodStart, PeriodEnd, "Bearer x", CancellationToken.None);

        var entry = Assert.Single(report.Vehicles);
        Assert.Equal(0, entry.UtilizationPercent);
        Assert.Equal(0, entry.CompletedTripCount);
        Assert.False(entry.HasInsufficientData);
    }

    [Fact]
    public async Task GetFleetUtilization_VehicleRegisteredAfterPeriodEnd_ExcludedFromReport()
    {
        var lateVehicle = Vehicle(Guid.NewGuid(), createdAt: PeriodEnd.AddDays(1));
        var fleet = new FakeFleetVehicleClient { Vehicles = [lateVehicle] };
        var trips = new FakeTripDispatchClient { CompletedTrips = [] };
        var service = BuildService(fleet, trips, new InMemoryTrackingRepository());

        var report = await service.GetFleetUtilizationAsync(PeriodStart, PeriodEnd, "Bearer x", CancellationToken.None);

        Assert.Empty(report.Vehicles);
        Assert.Equal(0, report.Summary.TotalVehicles);
    }

    [Fact]
    public async Task GetFleetUtilization_TripLifecycleEntirelyOutsidePeriod_NeverQueriesLocationHistory()
    {
        // Pre-filter check: a trip that finished long before the period
        // started shouldn't even trigger a history lookup.
        var tripId = Guid.NewGuid();
        var fleet = new FakeFleetVehicleClient { Vehicles = [Vehicle(VehicleId)] };
        var trips = new FakeTripDispatchClient
        {
            CompletedTrips = [CompletedTrip(tripId, VehicleId, PeriodStart.AddDays(-10), PeriodStart.AddDays(-9))]
        };
        var repository = new InMemoryTrackingRepository();
        repository.Seed(tripId, PeriodStart.AddDays(-10), PeriodStart.AddDays(-9)); // would wrongly count if queried
        var service = BuildService(fleet, trips, repository);

        var report = await service.GetFleetUtilizationAsync(PeriodStart, PeriodEnd, "Bearer x", CancellationToken.None);

        var entry = Assert.Single(report.Vehicles);
        Assert.Equal(0, entry.CompletedTripCount);
        Assert.Equal(0, entry.InUseHours);
        Assert.DoesNotContain(tripId, repository.RequestedTripIds);
    }

    [Fact]
    public async Task GetFleetUtilization_MultipleVehicles_SummaryUsesTotalHoursNotAverageOfPercentages()
    {
        var busyVehicle = Guid.NewGuid();
        var idleVehicle = Guid.NewGuid();
        var busyTrip = Guid.NewGuid();

        var fleet = new FakeFleetVehicleClient { Vehicles = [Vehicle(busyVehicle), Vehicle(idleVehicle)] };
        var trips = new FakeTripDispatchClient
        {
            CompletedTrips = [CompletedTrip(busyTrip, busyVehicle, PeriodStart.AddHours(1), PeriodStart.AddHours(169))]
        };
        var repository = new InMemoryTrackingRepository();
        // 168 hours of pings - the entire period - for the busy vehicle.
        repository.Seed(busyTrip, PeriodStart, PeriodEnd);
        var service = BuildService(fleet, trips, repository);

        var report = await service.GetFleetUtilizationAsync(PeriodStart, PeriodEnd, "Bearer x", CancellationToken.None);

        // Total in-use (168h) / total possible (2 vehicles * 168h = 336h) = 50%,
        // not the 75% a plain average of [100%, 0%] would (wrongly) suggest.
        Assert.Equal(50, report.Summary.OverallUtilizationPercent, 2);
        Assert.Equal(336, report.Summary.TotalPossibleHours, 2);
        Assert.Equal(168, report.Summary.TotalInUseHours, 2);
    }

    [Fact]
    public async Task GetFleetUtilization_FleetServiceUnreachable_PropagatesFailure()
    {
        var fleet = new FakeFleetVehicleClient
        {
            Exception = new FleetServiceUnavailableException("down", new HttpRequestException())
        };
        var service = BuildService(fleet, new FakeTripDispatchClient(), new InMemoryTrackingRepository());

        await Assert.ThrowsAsync<FleetServiceUnavailableException>(
            () => service.GetFleetUtilizationAsync(PeriodStart, PeriodEnd, "Bearer x", CancellationToken.None));
    }

    private static FleetVehicleSnapshot Vehicle(Guid id, DateTime? createdAt = null) =>
        new(id, "WP-ABC-1234", "Van", createdAt ?? PeriodStart.AddYears(-1));

    private static CompletedTripSnapshot CompletedTrip(Guid id, Guid vehicleId, DateTime createdAt, DateTime updatedAt) =>
        new(id, vehicleId, createdAt, updatedAt);

    private static FleetUtilizationService BuildService(
        FakeFleetVehicleClient? fleet = null,
        FakeTripDispatchClient? trips = null,
        InMemoryTrackingRepository? repository = null) =>
        new(fleet ?? new FakeFleetVehicleClient(), trips ?? new FakeTripDispatchClient(), repository ?? new InMemoryTrackingRepository());

    private sealed class FakeFleetVehicleClient : IFleetVehicleClient
    {
        public List<FleetVehicleSnapshot> Vehicles { get; set; } = [];
        public Exception? Exception { get; set; }

        public Task<List<FleetVehicleSnapshot>> GetAllVehiclesAsync(string? authorizationHeader, CancellationToken cancellationToken)
        {
            if (Exception is not null) throw Exception;
            return Task.FromResult(Vehicles);
        }
    }

    private sealed class FakeTripDispatchClient : ITripDispatchClient
    {
        public List<CompletedTripSnapshot> CompletedTrips { get; set; } = [];

        public Task<TripSnapshot?> GetTripAsync(Guid tripId, string? authorizationHeader, CancellationToken cancellationToken) =>
            throw new NotSupportedException("Not exercised by FleetUtilizationServiceTests.");

        public Task<List<FleetTripSnapshot>> GetInProgressTripsAsync(string? authorizationHeader, CancellationToken cancellationToken) =>
            throw new NotSupportedException("Not exercised by FleetUtilizationServiceTests.");

        public Task<List<CompletedTripSnapshot>> GetCompletedTripsAsync(string? authorizationHeader, CancellationToken cancellationToken) =>
            Task.FromResult(CompletedTrips);
    }

    private sealed class InMemoryTrackingRepository : ITrackingRepository
    {
        private readonly Dictionary<Guid, List<TripLocationUpdate>> _pingsByTrip = new();
        public List<Guid> RequestedTripIds { get; } = [];

        public void Seed(Guid tripId, params DateTime[] observedAtTimestamps) =>
            _pingsByTrip[tripId] = observedAtTimestamps.Select(t => new TripLocationUpdate
            {
                Id = Guid.NewGuid(), TripId = tripId, DriverId = DriverId, VehicleId = VehicleId,
                Latitude = 7m, Longitude = 80m, ObservedAt = t, ReceivedAt = t
            }).ToList();

        public Task CreateAsync(TripLocationUpdate update) =>
            throw new NotSupportedException("Not exercised by FleetUtilizationServiceTests.");

        public Task<List<TripLocationUpdate>> GetByTripIdAsync(Guid tripId, int limit)
        {
            RequestedTripIds.Add(tripId);
            return Task.FromResult(_pingsByTrip.TryGetValue(tripId, out var pings) ? pings.Take(limit).ToList() : []);
        }

        public Task<Dictionary<Guid, TripLocationUpdate>> GetLatestPerTripAsync(List<Guid> tripIds) =>
            throw new NotSupportedException("Not exercised by FleetUtilizationServiceTests.");
    }
}