using TripDispatchService.Data;
using TripDispatchService.Models;
using TripDispatchService.Services;

namespace TripDispatchService.Tests;

/// <summary>SCRUM-143/145 (US-18): TripCompletionService tested without MySQL.</summary>
public class TripCompletionServiceTests
{
    private static readonly DateTime PeriodStart = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime PeriodEnd = new(2026, 1, 8, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task GetTripCompletion_NullStart_ThrowsValidation()
    {
        var service = new TripCompletionService(new InMemoryTripRepository());

        var ex = await Assert.ThrowsAsync<ValidationException>(
            () => service.GetTripCompletionAsync(null, PeriodEnd, CancellationToken.None));

        Assert.Contains("start", ex.Errors.Keys);
    }

    [Fact]
    public async Task GetTripCompletion_NullEnd_ThrowsValidation()
    {
        var service = new TripCompletionService(new InMemoryTripRepository());

        var ex = await Assert.ThrowsAsync<ValidationException>(
            () => service.GetTripCompletionAsync(PeriodStart, null, CancellationToken.None));

        Assert.Contains("end", ex.Errors.Keys);
    }

    [Fact]
    public async Task GetTripCompletion_EndBeforeStart_ThrowsValidation()
    {
        var service = new TripCompletionService(new InMemoryTripRepository());

        var ex = await Assert.ThrowsAsync<ValidationException>(
            () => service.GetTripCompletionAsync(PeriodEnd, PeriodStart, CancellationToken.None));

        Assert.Contains("end", ex.Errors.Keys);
    }

    [Fact]
    public async Task GetTripCompletion_EndEqualsStart_ThrowsValidation()
    {
        // Deliberate extension beyond the AC's literal "end before start" -
        // a zero-length window isn't a meaningful period either, same rule
        // as FleetUtilizationService (SCRUM-137).
        var service = new TripCompletionService(new InMemoryTripRepository());

        var ex = await Assert.ThrowsAsync<ValidationException>(
            () => service.GetTripCompletionAsync(PeriodStart, PeriodStart, CancellationToken.None));

        Assert.Contains("end", ex.Errors.Keys);
    }

    [Fact]
    public async Task GetTripCompletion_NoEligibleTrips_ReturnsNoDataWithoutDivisionByZero()
    {
        // AC Scenario 3.
        var repository = new InMemoryTripRepository();
        var service = new TripCompletionService(repository);

        var result = await service.GetTripCompletionAsync(PeriodStart, PeriodEnd, CancellationToken.None);

        Assert.False(result.HasData);
        Assert.Equal(0, result.EligibleTripCount);
        Assert.Equal(0, result.CompletionRatePercent);
    }

    [Fact]
    public async Task GetTripCompletion_MixOfStatuses_ClassifiesEachTripExactlyOnce()
    {
        // AC Scenario 1 & 4.
        var repository = new InMemoryTripRepository();
        repository.Trips.Add(NewTrip(TripStatus.Requested, Mid()));
        repository.Trips.Add(NewTrip(TripStatus.Assigned, Mid()));
        repository.Trips.Add(NewTrip(TripStatus.InProgress, Mid()));
        repository.Trips.Add(NewTrip(TripStatus.Completed, Mid()));
        repository.Trips.Add(NewTrip(TripStatus.Cancelled, Mid()));
        var service = new TripCompletionService(repository);

        var result = await service.GetTripCompletionAsync(PeriodStart, PeriodEnd, CancellationToken.None);

        Assert.Equal(5, result.EligibleTripCount);
        Assert.Equal(1, result.RequestedCount);
        Assert.Equal(1, result.AssignedCount);
        Assert.Equal(1, result.InProgressCount);
        Assert.Equal(1, result.CompletedCount);
        Assert.Equal(1, result.CancelledCount);
        // Every trip lands in exactly one bucket.
        Assert.Equal(result.EligibleTripCount,
            result.RequestedCount + result.AssignedCount + result.InProgressCount +
            result.CompletedCount + result.CancelledCount);
    }

    [Fact]
    public async Task GetTripCompletion_CompletionRate_UsesCompletedOverEligibleIncludingCancelled()
    {
        // AC Scenario 2. Confirmed rule: cancelled trips stay in the
        // denominator - 2 completed out of 4 eligible (including 1
        // cancelled) is 50%, not 2/3 (which would ignore the cancellation).
        var repository = new InMemoryTripRepository();
        repository.Trips.Add(NewTrip(TripStatus.Completed, Mid()));
        repository.Trips.Add(NewTrip(TripStatus.Completed, Mid()));
        repository.Trips.Add(NewTrip(TripStatus.Cancelled, Mid()));
        repository.Trips.Add(NewTrip(TripStatus.Requested, Mid()));
        var service = new TripCompletionService(repository);

        var result = await service.GetTripCompletionAsync(PeriodStart, PeriodEnd, CancellationToken.None);

        Assert.Equal(4, result.EligibleTripCount);
        Assert.Equal(2, result.CompletedCount);
        Assert.Equal(50, result.CompletionRatePercent);
    }

    [Fact]
    public async Task GetTripCompletion_PeriodMembershipUsesCreatedAt_NotCurrentStatus()
    {
        // A trip created BEFORE the period is excluded even though it is
        // now Completed - period membership is about when the trip was
        // created, never about its current lifecycle state.
        var repository = new InMemoryTripRepository();
        repository.Trips.Add(NewTrip(TripStatus.Completed, PeriodStart.AddDays(-10)));
        repository.Trips.Add(NewTrip(TripStatus.Requested, Mid())); // inside the period
        var service = new TripCompletionService(repository);

        var result = await service.GetTripCompletionAsync(PeriodStart, PeriodEnd, CancellationToken.None);

        Assert.Equal(1, result.EligibleTripCount);
        Assert.Equal(1, result.RequestedCount);
        Assert.Equal(0, result.CompletedCount);
    }

    [Fact]
    public async Task GetTripCompletion_TripsOutsidePeriod_AreExcludedFromEligibleCount()
    {
        var repository = new InMemoryTripRepository();
        repository.Trips.Add(NewTrip(TripStatus.Completed, PeriodStart.AddDays(-1))); // before
        repository.Trips.Add(NewTrip(TripStatus.Completed, PeriodEnd.AddDays(1)));    // after
        repository.Trips.Add(NewTrip(TripStatus.Completed, Mid()));                   // inside
        var service = new TripCompletionService(repository);

        var result = await service.GetTripCompletionAsync(PeriodStart, PeriodEnd, CancellationToken.None);

        Assert.Equal(1, result.EligibleTripCount);
    }

    [Fact]
    public async Task GetTripCompletion_PassesRequestedPeriodStraightToRepository()
    {
        var repository = new InMemoryTripRepository();
        var service = new TripCompletionService(repository);

        await service.GetTripCompletionAsync(PeriodStart, PeriodEnd, CancellationToken.None);

        Assert.Equal(PeriodStart, repository.LastRequestedStart);
        Assert.Equal(PeriodEnd, repository.LastRequestedEnd);
    }

    private static DateTime Mid() => PeriodStart.AddDays(3);

    private static Trip NewTrip(string status, DateTime createdAt) => new()
    {
        Id = Guid.NewGuid(),
        PickupLocation = "Colombo Fort",
        DropoffLocation = "Kandy",
        RequiredCapacity = "4 persons",
        Status = status,
        CreatedAt = createdAt,
        UpdatedAt = createdAt,
    };

    private sealed class InMemoryTripRepository : ITripRepository
    {
        public List<Trip> Trips { get; } = new();
        public DateTime? LastRequestedStart { get; private set; }
        public DateTime? LastRequestedEnd { get; private set; }

        public Task CreateTripAsync(Trip trip) =>
            throw new NotSupportedException("Not exercised by TripCompletionServiceTests.");

        public Task<List<Trip>> GetAllAsync(string? status) =>
            throw new NotSupportedException("Not exercised by TripCompletionServiceTests.");

        public Task<Trip?> GetByIdAsync(Guid id) =>
            throw new NotSupportedException("Not exercised by TripCompletionServiceTests.");

        public Task UpdateAsync(Trip trip) =>
            throw new NotSupportedException("Not exercised by TripCompletionServiceTests.");

        public Task<List<Trip>> GetByDriverIdAsync(Guid driverId) =>
            throw new NotSupportedException("Not exercised by TripCompletionServiceTests.");

        public Task<List<Trip>> GetByCreatedDateRangeAsync(DateTime start, DateTime end)
        {
            LastRequestedStart = start;
            LastRequestedEnd = end;
            var result = Trips.Where(t => t.CreatedAt >= start && t.CreatedAt <= end).ToList();
            return Task.FromResult(result);
        }
    }
}