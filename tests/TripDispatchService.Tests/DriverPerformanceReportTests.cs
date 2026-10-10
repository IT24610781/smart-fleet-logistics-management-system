using Microsoft.AspNetCore.Authorization;
using TripDispatchService.Controllers;
using TripDispatchService.Data;
using TripDispatchService.DTOs;
using TripDispatchService.Models;
using TripDispatchService.Services;

namespace TripDispatchService.Tests;

/// <summary>
/// SCRUM-150 (US-19): Driver Performance Report tests - metric calculation,
/// rejection/reassignment attribution, period boundaries, zero/incomplete
/// data, and that the endpoint is restricted to Dispatcher/Admin.
/// </summary>
public class DriverPerformanceReportTests
{
    private static readonly DateTime PeriodStart = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime PeriodEnd = new(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc);

    // Validation 

    [Fact]
    public async Task MissingStart_ThrowsValidation()
    {
        var service = new DriverPerformanceService(new FakeDriverDirectoryClient(), new FakeTripRepository());

        var ex = await Assert.ThrowsAsync<ValidationException>(
            () => service.GetDriverPerformanceAsync(null, PeriodEnd, null, default));

        Assert.Contains("start", ex.Errors.Keys);
    }

    [Fact]
    public async Task MissingEnd_ThrowsValidation()
    {
        var service = new DriverPerformanceService(new FakeDriverDirectoryClient(), new FakeTripRepository());

        var ex = await Assert.ThrowsAsync<ValidationException>(
            () => service.GetDriverPerformanceAsync(PeriodStart, null, null, default));

        Assert.Contains("end", ex.Errors.Keys);
    }

    [Theory]
    [InlineData(0)]  // end == start
    [InlineData(-1)] // end before start
    public async Task EndNotAfterStart_ThrowsValidation(int dayOffset)
    {
        var service = new DriverPerformanceService(new FakeDriverDirectoryClient(), new FakeTripRepository());
        var end = PeriodStart.AddDays(dayOffset);

        var ex = await Assert.ThrowsAsync<ValidationException>(
            () => service.GetDriverPerformanceAsync(PeriodStart, end, null, default));

        Assert.Contains("end", ex.Errors.Keys);
    }

    // ----- Metric calculation -----

    [Fact]
    public async Task MetricsAreCalculatedPerDriver_AcceptedCompletedRejected()
    {
        var driverA = Guid.NewGuid();
        var driverB = Guid.NewGuid();

        var repository = new FakeTripRepository
        {
            Trips =
            [
                // Driver A: one In-Progress (counts as accepted, not completed),
                // one Completed (counts as both accepted and completed).
                Trip(driverA, TripStatus.InProgress, inPeriod: true),
                Trip(driverA, TripStatus.Completed, inPeriod: true),

                // Driver B: rejected one trip, no accepted/completed activity.
                RejectedTrip(driverB, inPeriod: true),

                // Noise: a trip still sitting at Requested/Assigned should not
                // count as accepted for anyone.
                Trip(null, TripStatus.Requested, inPeriod: true)
            ]
        };

        var directory = new FakeDriverDirectoryClient
        {
            Drivers =
            [
                new DriverSummary(driverA, "Ada", "Driver", true),
                new DriverSummary(driverB, "Beth", "Driver", true)
            ]
        };

        var service = new DriverPerformanceService(directory, repository);

        var report = await service.GetDriverPerformanceAsync(PeriodStart, PeriodEnd, null, default);

        var entryA = Assert.Single(report.Drivers, d => d.DriverId == driverA);
        Assert.Equal(2, entryA.AcceptedCount);
        Assert.Equal(1, entryA.CompletedCount);
        Assert.Equal(0, entryA.RejectedCount);
        Assert.True(entryA.HasActivity);

        var entryB = Assert.Single(report.Drivers, d => d.DriverId == driverB);
        Assert.Equal(0, entryB.AcceptedCount);
        Assert.Equal(0, entryB.CompletedCount);
        Assert.Equal(1, entryB.RejectedCount);
        Assert.True(entryB.HasActivity);
    }

    [Fact]
    public async Task HasActivityFalse_WhenDriverHasNoTripsInPeriod()
    {
        var idleDriver = Guid.NewGuid();
        var directory = new FakeDriverDirectoryClient
        {
            Drivers = [new DriverSummary(idleDriver, "Idle", "Driver", true)]
        };
        var service = new DriverPerformanceService(directory, new FakeTripRepository());

        var report = await service.GetDriverPerformanceAsync(PeriodStart, PeriodEnd, null, default);

        var entry = Assert.Single(report.Drivers);
        Assert.Equal(0, entry.AcceptedCount);
        Assert.Equal(0, entry.CompletedCount);
        Assert.Equal(0, entry.RejectedCount);
        Assert.False(entry.HasActivity);
    }

    // Rejection / reassignment attribution (the SCRUM-148 fix)

    [Fact]
    public async Task RejectionSurvivesReassignment_OriginalDriverStillCreditedWithRejection()
    {
        // Exercises the real TripService state machine, not just the report:
        // vehicle assigned to Driver A -> Driver A rejects -> trip reassigned
        // to Driver B -> Driver B accepts. Driver A's rejection must still be
        // attributed after the reassignment overwrote AssignedDriverId.
        var driverA = Guid.NewGuid();
        var driverB = Guid.NewGuid();
        var trip = new Trip
        {
            Id = Guid.NewGuid(),
            Status = TripStatus.Requested,
            CreatedAt = PeriodStart.AddDays(1),
            UpdatedAt = PeriodStart.AddDays(1)
        };
        var tripRepository = new FakeTripRepository { Trips = [trip] };
        var tripService = new TripService(tripRepository, new FakeFleetVehicleClient());

        await tripService.AssignVehicleAsync(trip.Id, Guid.NewGuid(), driverA, null);
        await tripService.RespondAsync(trip.Id, driverA, "reject", null);
        await tripService.AssignVehicleAsync(trip.Id, Guid.NewGuid(), driverB, null);
        await tripService.RespondAsync(trip.Id, driverB, "accept", null);

        Assert.Equal(driverA, trip.RejectedByDriverId);
        Assert.Equal(driverB, trip.AssignedDriverId);

        var directory = new FakeDriverDirectoryClient
        {
            Drivers =
            [
                new DriverSummary(driverA, "Ada", "Driver", true),
                new DriverSummary(driverB, "Beth", "Driver", true)
            ]
        };
        var reportService = new DriverPerformanceService(directory, tripRepository);

        var report = await reportService.GetDriverPerformanceAsync(PeriodStart, PeriodEnd, null, default);

        var entryA = Assert.Single(report.Drivers, d => d.DriverId == driverA);
        Assert.Equal(1, entryA.RejectedCount);
        Assert.Equal(0, entryA.AcceptedCount);

        var entryB = Assert.Single(report.Drivers, d => d.DriverId == driverB);
        Assert.Equal(1, entryB.AcceptedCount);
        Assert.Equal(0, entryB.RejectedCount);
    }

    // Period boundaries

    [Fact]
    public async Task PeriodBoundary_StartIsInclusive_EndIsExclusive()
    {
        var driver = Guid.NewGuid();
        var repository = new FakeTripRepository
        {
            Trips =
            [
                CreatedAt(driver, PeriodStart),             // exactly on start - included
                CreatedAt(driver, PeriodEnd),                // exactly on end - excluded
                CreatedAt(driver, PeriodStart.AddDays(-1)),  // before period - excluded
                CreatedAt(driver, PeriodEnd.AddDays(-1))     // just before end - included
            ]
        };
        var directory = new FakeDriverDirectoryClient
        {
            Drivers = [new DriverSummary(driver, "Cal", "Driver", true)]
        };
        var service = new DriverPerformanceService(directory, repository);

        var report = await service.GetDriverPerformanceAsync(PeriodStart, PeriodEnd, null, default);

        var entry = Assert.Single(report.Drivers);
        // Both in-range trips are Completed, so they land in AcceptedCount too.
        Assert.Equal(2, entry.CompletedCount);
    }

    // Zero activity / incomplete historical data

    [Fact]
    public async Task TripsReferencingAnUnknownDriverId_AreIgnoredWithoutError()
    {
        // Simulates incomplete historical data: a trip whose driver reference
        // no longer resolves to any account the directory returns (e.g. the
        // account was deleted). The report must not error or fabricate an
        // entry for it - it simply contributes nothing.
        var orphanDriverId = Guid.NewGuid();
        var repository = new FakeTripRepository
        {
            Trips = [Trip(orphanDriverId, TripStatus.Completed, inPeriod: true)]
        };
        var directory = new FakeDriverDirectoryClient { Drivers = [] };
        var service = new DriverPerformanceService(directory, repository);

        var report = await service.GetDriverPerformanceAsync(PeriodStart, PeriodEnd, null, default);

        Assert.Empty(report.Drivers);
    }

    [Fact]
    public async Task NoDriversAtAll_ReturnsEmptyReportWithoutError()
    {
        var service = new DriverPerformanceService(
            new FakeDriverDirectoryClient { Drivers = [] },
            new FakeTripRepository());

        var report = await service.GetDriverPerformanceAsync(PeriodStart, PeriodEnd, null, default);

        Assert.Empty(report.Drivers);
        Assert.Equal(PeriodStart, report.PeriodStart);
        Assert.Equal(PeriodEnd, report.PeriodEnd);
    }

    [Fact]
    public async Task DriverDirectoryUnavailable_PropagatesAsDriverDirectoryUnavailableException()
    {
        var service = new DriverPerformanceService(
            new FakeDriverDirectoryClient { ThrowUnavailable = true },
            new FakeTripRepository());

        await Assert.ThrowsAsync<DriverDirectoryUnavailableException>(
            () => service.GetDriverPerformanceAsync(PeriodStart, PeriodEnd, "Bearer dispatcher", default));
    }

    // Authorization (unauthorized users cannot retrieve restricted reports)

    [Fact]
    public void DriverPerformanceEndpoint_RestrictsToDispatcherAndAdmin()
    {
        var method = typeof(ReportsController).GetMethod(nameof(ReportsController.GetDriverPerformance));
        var attribute = method?.GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
            .Cast<AuthorizeAttribute>()
            .FirstOrDefault();

        Assert.NotNull(attribute);
        var roles = attribute!.Roles?.Split(',') ?? [];
        Assert.Contains("Dispatcher", roles);
        Assert.Contains("Admin", roles);
        Assert.DoesNotContain("Driver", roles);
    }

    // Helpers

    private static Trip Trip(Guid? driverId, string status, bool inPeriod) => new()
    {
        Id = Guid.NewGuid(),
        AssignedDriverId = driverId,
        Status = status,
        CreatedAt = inPeriod ? PeriodStart.AddDays(5) : PeriodStart.AddDays(-5),
        UpdatedAt = inPeriod ? PeriodStart.AddDays(5) : PeriodStart.AddDays(-5)
    };

    private static Trip RejectedTrip(Guid rejectedByDriverId, bool inPeriod) => new()
    {
        Id = Guid.NewGuid(),
        RejectedByDriverId = rejectedByDriverId,
        Status = TripStatus.Requested,
        CreatedAt = inPeriod ? PeriodStart.AddDays(5) : PeriodStart.AddDays(-5),
        UpdatedAt = inPeriod ? PeriodStart.AddDays(5) : PeriodStart.AddDays(-5)
    };

    private static Trip CreatedAt(Guid driverId, DateTime createdAt) => new()
    {
        Id = Guid.NewGuid(),
        AssignedDriverId = driverId,
        Status = TripStatus.Completed,
        CreatedAt = createdAt,
        UpdatedAt = createdAt
    };

    private sealed class FakeTripRepository : ITripRepository
    {
        public List<Trip> Trips { get; set; } = [];

        public Task CreateTripAsync(Trip trip)
        {
            Trips.Add(trip);
            return Task.CompletedTask;
        }

        public Task<List<Trip>> GetAllAsync(string? status) =>
            Task.FromResult(status is null ? Trips : Trips.Where(t => t.Status == status).ToList());

        public Task<Trip?> GetByIdAsync(Guid id) =>
            Task.FromResult(Trips.FirstOrDefault(t => t.Id == id));

        public Task UpdateAsync(Trip trip) => Task.CompletedTask;

        public Task<List<Trip>> GetByDriverIdAsync(Guid driverId) =>
            Task.FromResult(Trips.Where(t => t.AssignedDriverId == driverId).ToList());

        public Task<List<Trip>> GetByCreatedDateRangeAsync(DateTime start, DateTime end) =>
            Task.FromResult(Trips.Where(t => t.CreatedAt >= start && t.CreatedAt < end).ToList());
    }

    private sealed class FakeDriverDirectoryClient : IDriverDirectoryClient
    {
        public List<DriverSummary> Drivers { get; set; } = [];
        public bool ThrowUnavailable { get; set; }

        public Task<List<DriverSummary>> GetDriversAsync(string? authorizationHeader, CancellationToken cancellationToken)
        {
            if (ThrowUnavailable)
            {
                throw new DriverDirectoryUnavailableException(
                    "Driver accounts could not be retrieved. The performance report could not be generated.",
                    new HttpRequestException());
            }

            return Task.FromResult(Drivers);
        }
    }

    private sealed class FakeFleetVehicleClient : IFleetVehicleClient
    {
        public Task<List<FleetVehicleDto>> GetVehiclesAsync(string? authorizationHeader) =>
            Task.FromResult(new List<FleetVehicleDto>());

        public Task SetVehicleStatusAsync(Guid vehicleId, string status, string? authorizationHeader) =>
            Task.CompletedTask;
    }
}