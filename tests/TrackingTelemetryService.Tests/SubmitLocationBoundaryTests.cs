using System.Globalization;
using TrackingTelemetryService.Data;
using TrackingTelemetryService.DTOs;
using TrackingTelemetryService.Models;
using TrackingTelemetryService.Services;

namespace TrackingTelemetryService.Tests;

/// <summary>
/// SCRUM-153 / US-14 (Sprint 4 QA): boundary-value and rule tests for
/// TrackingService.SubmitLocationAsync. Complements TrackingWorkflowTests, which covers
/// one invalid value per field; this file checks the exact edges of every rule:
///   latitude -90..90, longitude -180..180,
///   observedAt no more than 2 minutes in the future and no more than 24 hours in the past,
///   only an In-Progress trip with a vehicle, and only the assigned driver's own trip.
/// No MySQL, HTTP or Docker needed. Each [InlineData] row runs and reports as its own test.
/// </summary>
[Trait("Category", "Unit")]
public class SubmitLocationBoundaryTests
{
    private static readonly Guid DriverId = Guid.NewGuid();
    private static readonly Guid OtherDriverId = Guid.NewGuid();
    private static readonly Guid TripId = Guid.NewGuid();
    private static readonly Guid VehicleId = Guid.NewGuid();

    private static TripSnapshot InProgressTrip() => new(TripId, "In-Progress", DriverId, VehicleId);

    private static SubmitLocationRequest Request(
        string latitude = "7.2513", string longitude = "80.3464", int observedOffsetSeconds = 0) => new()
    {
        TripId = TripId,
        Latitude = decimal.Parse(latitude, CultureInfo.InvariantCulture),
        Longitude = decimal.Parse(longitude, CultureInfo.InvariantCulture),
        ObservedAt = DateTime.UtcNow.AddSeconds(observedOffsetSeconds)
    };

    private static (TrackingService Service, RecordingRepository Repository, StubTripClient Trips) Build(TripSnapshot? trip)
    {
        var repository = new RecordingRepository();
        var trips = new StubTripClient { Result = trip };
        return (new TrackingService(repository, trips), repository, trips);
    }

    // ------------------------------------------------------------------ latitude

    [Theory]
    [InlineData("-90")]
    [InlineData("-89.999999")]
    [InlineData("0")]
    [InlineData("7.2513")]
    [InlineData("89.999999")]
    [InlineData("90")]
    public async Task Latitude_AtOrInsideRange_IsAccepted(string latitude)
    {
        var (service, repository, _) = Build(InProgressTrip());

        await service.SubmitLocationAsync(DriverId, Request(latitude: latitude), "Bearer x", CancellationToken.None);

        Assert.Equal(1, repository.Writes);
    }

    [Theory]
    [InlineData("-90.000001")]
    [InlineData("-91")]
    [InlineData("-180")]
    [InlineData("90.000001")]
    [InlineData("91")]
    [InlineData("1000")]
    public async Task Latitude_OutsideRange_IsRejectedAndNothingSaved(string latitude)
    {
        var (service, repository, trips) = Build(InProgressTrip());

        var ex = await Assert.ThrowsAsync<ValidationException>(
            () => service.SubmitLocationAsync(DriverId, Request(latitude: latitude), "Bearer x", CancellationToken.None));

        Assert.True(ex.Errors.ContainsKey("latitude"));
        Assert.Equal(0, repository.Writes);
        Assert.Equal(0, trips.CallCount);
    }

    // ----------------------------------------------------------------- longitude

    [Theory]
    [InlineData("-180")]
    [InlineData("-179.999999")]
    [InlineData("0")]
    [InlineData("80.3464")]
    [InlineData("179.999999")]
    [InlineData("180")]
    public async Task Longitude_AtOrInsideRange_IsAccepted(string longitude)
    {
        var (service, repository, _) = Build(InProgressTrip());

        await service.SubmitLocationAsync(DriverId, Request(longitude: longitude), "Bearer x", CancellationToken.None);

        Assert.Equal(1, repository.Writes);
    }

    [Theory]
    [InlineData("-180.000001")]
    [InlineData("-181")]
    [InlineData("-360")]
    [InlineData("180.000001")]
    [InlineData("181")]
    [InlineData("1000")]
    public async Task Longitude_OutsideRange_IsRejectedAndNothingSaved(string longitude)
    {
        var (service, repository, trips) = Build(InProgressTrip());

        var ex = await Assert.ThrowsAsync<ValidationException>(
            () => service.SubmitLocationAsync(DriverId, Request(longitude: longitude), "Bearer x", CancellationToken.None));

        Assert.True(ex.Errors.ContainsKey("longitude"));
        Assert.Equal(0, repository.Writes);
        Assert.Equal(0, trips.CallCount);
    }

    // --------------------------------------------------------------- observedAt
    // Rules: at most 2 minutes (120 s) in the future, at most 24 hours (86400 s) in the past.
    // Every row keeps a 10 s or larger safety margin so the test is not flaky.

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-60)]
    [InlineData(-3600)]
    [InlineData(-86300)] // 100 s inside the 24 h limit
    [InlineData(30)]
    [InlineData(100)]    // 20 s inside the 2 min future limit
    public async Task ObservedAt_InsideAllowedWindow_IsAccepted(int offsetSeconds)
    {
        var (service, repository, _) = Build(InProgressTrip());

        await service.SubmitLocationAsync(
            DriverId, Request(observedOffsetSeconds: offsetSeconds), "Bearer x", CancellationToken.None);

        Assert.Equal(1, repository.Writes);
    }

    [Theory]
    [InlineData(130)]     // 10 s past the future limit
    [InlineData(300)]
    [InlineData(3600)]
    [InlineData(86400)]
    [InlineData(-86500)]  // 100 s past the 24 h limit
    [InlineData(-90000)]
    [InlineData(-172800)]
    public async Task ObservedAt_OutsideAllowedWindow_IsRejectedAndNothingSaved(int offsetSeconds)
    {
        var (service, repository, trips) = Build(InProgressTrip());

        var ex = await Assert.ThrowsAsync<ValidationException>(
            () => service.SubmitLocationAsync(
                DriverId, Request(observedOffsetSeconds: offsetSeconds), "Bearer x", CancellationToken.None));

        Assert.True(ex.Errors.ContainsKey("observedAt"));
        Assert.Equal(0, repository.Writes);
        Assert.Equal(0, trips.CallCount);
    }

    // ------------------------------------------------------- several bad fields

    [Fact]
    public async Task AllFieldsMissing_ReportsEveryProblemAtOnce()
    {
        var (service, repository, trips) = Build(InProgressTrip());

        var ex = await Assert.ThrowsAsync<ValidationException>(
            () => service.SubmitLocationAsync(DriverId, new SubmitLocationRequest(), "Bearer x", CancellationToken.None));

        Assert.Equal(4, ex.Errors.Count);
        Assert.True(ex.Errors.ContainsKey("tripId"));
        Assert.True(ex.Errors.ContainsKey("latitude"));
        Assert.True(ex.Errors.ContainsKey("longitude"));
        Assert.True(ex.Errors.ContainsKey("observedAt"));
        Assert.Equal(0, repository.Writes);
        Assert.Equal(0, trips.CallCount);
    }

    [Fact]
    public async Task InvalidInput_IsReportedBeforeTripOwnershipIsChecked()
    {
        // A bad latitude on someone else's trip must give a validation error, not a "not found",
        // and must not even contact the Trip service.
        var (service, repository, trips) = Build(new TripSnapshot(TripId, "In-Progress", OtherDriverId, VehicleId));

        await Assert.ThrowsAsync<ValidationException>(
            () => service.SubmitLocationAsync(DriverId, Request(latitude: "95"), "Bearer x", CancellationToken.None));

        Assert.Equal(0, repository.Writes);
        Assert.Equal(0, trips.CallCount);
    }

    // ---------------------------------------------------------------- trip rules

    [Theory]
    [InlineData("Requested")]
    [InlineData("Assigned")]
    [InlineData("Completed")]
    [InlineData("Cancelled")]
    [InlineData("Pending")]
    public async Task TripNotInProgress_IsRejectedAndMentionsTheStatus(string status)
    {
        var (service, repository, _) = Build(new TripSnapshot(TripId, status, DriverId, VehicleId));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.SubmitLocationAsync(DriverId, Request(), "Bearer x", CancellationToken.None));

        Assert.Contains(status, ex.Message);
        Assert.Equal(0, repository.Writes);
    }

    [Fact]
    public async Task InProgressTripWithoutAVehicle_IsRejectedAndNothingSaved()
    {
        var (service, repository, _) = Build(new TripSnapshot(TripId, "In-Progress", DriverId, null));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.SubmitLocationAsync(DriverId, Request(), "Bearer x", CancellationToken.None));

        Assert.Equal(0, repository.Writes);
    }

    [Fact]
    public async Task TripWithNoAssignedDriver_ReportsNotFoundInsteadOfCrashing()
    {
        var (service, repository, _) = Build(new TripSnapshot(TripId, "In-Progress", null, VehicleId));

        await Assert.ThrowsAsync<NotFoundException>(
            () => service.SubmitLocationAsync(DriverId, Request(), "Bearer x", CancellationToken.None));

        Assert.Equal(0, repository.Writes);
    }

    [Fact]
    public async Task EmptyGuidTripId_ReportsNotFound()
    {
        var (service, repository, trips) = Build(null);
        var request = Request();
        request.TripId = Guid.Empty;

        await Assert.ThrowsAsync<NotFoundException>(
            () => service.SubmitLocationAsync(DriverId, request, "Bearer x", CancellationToken.None));

        Assert.Equal(1, trips.CallCount);
        Assert.Equal(0, repository.Writes);
    }

    [Fact]
    public async Task MissingTripAndAnotherDriversTrip_GiveTheSameMessage()
    {
        // A driver must not be able to tell "trip does not exist" from "trip belongs to someone else".
        var (missingService, _, _) = Build(null);
        var (foreignService, _, _) = Build(new TripSnapshot(TripId, "In-Progress", OtherDriverId, VehicleId));

        var missing = await Assert.ThrowsAsync<NotFoundException>(
            () => missingService.SubmitLocationAsync(DriverId, Request(), null, CancellationToken.None));
        var foreign = await Assert.ThrowsAsync<NotFoundException>(
            () => foreignService.SubmitLocationAsync(DriverId, Request(), null, CancellationToken.None));

        Assert.Equal(missing.Message, foreign.Message);
    }

    // ------------------------------------------------------------- what is saved

    [Fact]
    public async Task AuthorizationHeader_IsForwardedUnchangedToTheTripService()
    {
        var (service, _, trips) = Build(InProgressTrip());

        await service.SubmitLocationAsync(DriverId, Request(), "Bearer abc.def.ghi", CancellationToken.None);

        Assert.Equal("Bearer abc.def.ghi", trips.LastAuthorizationHeader);
    }

    [Fact]
    public async Task SuccessfulSubmit_StoresExactCoordinatesTimeAndAssociations()
    {
        var (service, repository, _) = Build(InProgressTrip());
        var observed = DateTime.UtcNow.AddMinutes(-3);
        var before = DateTime.UtcNow.AddSeconds(-1);

        var result = await service.SubmitLocationAsync(DriverId, new SubmitLocationRequest
        {
            TripId = TripId, Latitude = 6.927079m, Longitude = 79.861244m, ObservedAt = observed
        }, "Bearer x", CancellationToken.None);

        var stored = Assert.Single(repository.Rows);
        Assert.Equal(6.927079m, stored.Latitude);
        Assert.Equal(79.861244m, stored.Longitude);
        Assert.Equal(observed, stored.ObservedAt);
        Assert.Equal(TripId, stored.TripId);
        Assert.Equal(DriverId, stored.DriverId);
        Assert.Equal(VehicleId, stored.VehicleId);
        Assert.NotEqual(Guid.Empty, stored.Id);
        Assert.InRange(stored.ReceivedAt, before, DateTime.UtcNow.AddSeconds(1));

        // The response mirrors what was stored.
        Assert.Equal(stored.Id, result.Id);
        Assert.Equal(stored.TripId, result.TripId);
        Assert.Equal(stored.ReceivedAt, result.ReceivedAt);
    }

    [Fact]
    public async Task TwoSubmissions_GetDifferentIds()
    {
        var (service, repository, _) = Build(InProgressTrip());

        var first = await service.SubmitLocationAsync(DriverId, Request(), "Bearer x", CancellationToken.None);
        var second = await service.SubmitLocationAsync(DriverId, Request(), "Bearer x", CancellationToken.None);

        Assert.NotEqual(first.Id, second.Id);
        Assert.Equal(2, repository.Writes);
    }

    // ------------------------------------------------------------- test doubles

    private sealed class RecordingRepository : ITrackingRepository
    {
        public readonly List<TripLocationUpdate> Rows = new();
        public int Writes => Rows.Count;

        public Task CreateAsync(TripLocationUpdate update)
        {
            Rows.Add(update);
            return Task.CompletedTask;
        }

        public Task<List<TripLocationUpdate>> GetByTripIdAsync(Guid tripId, int limit) =>
            Task.FromResult(Rows.Where(r => r.TripId == tripId).Take(limit).ToList());

        public Task<Dictionary<Guid, TripLocationUpdate>> GetLatestPerTripAsync(List<Guid> tripIds) =>
            throw new NotSupportedException("Not used by these tests.");
    }

    private sealed class StubTripClient : ITripDispatchClient
    {
        public TripSnapshot? Result { get; set; }
        public int CallCount { get; private set; }
        public string? LastAuthorizationHeader { get; private set; }

        public Task<TripSnapshot?> GetTripAsync(Guid tripId, string? authorizationHeader, CancellationToken cancellationToken)
        {
            CallCount++;
            LastAuthorizationHeader = authorizationHeader;
            return Task.FromResult(Result);
        }

        public Task<List<FleetTripSnapshot>> GetInProgressTripsAsync(string? authorizationHeader, CancellationToken cancellationToken) =>
            throw new NotSupportedException("Not used by these tests.");

        public Task<List<CompletedTripSnapshot>> GetCompletedTripsAsync(string? authorizationHeader, CancellationToken cancellationToken) =>
            throw new NotSupportedException("Not used by these tests.");
    }
}
