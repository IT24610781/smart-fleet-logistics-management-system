using System.Net;

namespace TrackingTelemetryService.Tests;

/// <summary>
/// SCRUM-153 / US-14: "Driver sends a live location update", tested through the real controller, JWT roles,
/// Trip service and database. Opt-in: see LiveApiHarness.cs for the environment variables.
///   dotnet test tests/TrackingTelemetryService.Tests --filter "Story=US-14&amp;Category=LiveApi"
/// L14_08 is an exploratory probe: it is expected to FAIL if local times without an offset are rejected
/// (see the comment on that test). A failure there is a bug candidate, not a broken test.
/// </summary>
[Trait("Story", "US-14")]
[Trait("Category", "LiveApi")]
public class Us14LiveApiTests
{
    [LiveApiFact]
    public async Task L14_01_NoToken_Returns401()
    {
        var trip = await LiveApi.StartTripAsync();

        var result = await LiveApi.PostPingAsync(trip.TripId, null, 6.9271, 79.8612, DateTime.UtcNow);

        LiveApi.Expect(result, HttpStatusCode.Unauthorized);
    }

    [LiveApiFact]
    public async Task L14_02_DispatcherToken_Returns403()
    {
        var trip = await LiveApi.StartTripAsync();

        var result = await LiveApi.PostPingAsync(trip.TripId, trip.DispatcherToken, 6.9271, 79.8612, DateTime.UtcNow);

        LiveApi.Expect(result, HttpStatusCode.Forbidden);
    }

    [LiveApiFact]
    public async Task L14_03_AdminToken_Returns403()
    {
        var trip = await LiveApi.StartTripAsync();
        var admin = await LiveApi.AdminTokenAsync();

        var result = await LiveApi.PostPingAsync(trip.TripId, admin, 6.9271, 79.8612, DateTime.UtcNow);

        LiveApi.Expect(result, HttpStatusCode.Forbidden);
    }

    [LiveApiFact]
    public async Task L14_04_DriverOwnInProgressTrip_Returns201WithStoredValues()
    {
        var trip = await LiveApi.StartTripAsync();
        var observed = DateTime.UtcNow.AddSeconds(-10);

        var result = await LiveApi.PostPingAsync(trip.TripId, trip.Driver.Token, 6.9271, 79.8612, observed);

        LiveApi.Expect(result, HttpStatusCode.Created);
        Assert.Equal(trip.TripId, result.Json["tripId"]!.GetValue<string>());
        Assert.Equal(6.9271m, result.Json["latitude"]!.GetValue<decimal>());
        Assert.Equal(79.8612m, result.Json["longitude"]!.GetValue<decimal>());
        Assert.False(string.IsNullOrEmpty(result.Json["id"]!.GetValue<string>()));
    }

    [LiveApiFact]
    public async Task L14_05_AnotherDriversTrip_Returns404()
    {
        var trip = await LiveApi.StartTripAsync();
        var otherDriver = await LiveApi.CreateDriverAsync(await LiveApi.AdminTokenAsync());

        var result = await LiveApi.PostPingAsync(trip.TripId, otherDriver.Token, 6.9271, 79.8612, DateTime.UtcNow);

        LiveApi.Expect(result, HttpStatusCode.NotFound);
    }

    [LiveApiFact]
    public async Task L14_06_LatitudeOutOfRange_Returns400WithLatitudeError()
    {
        var trip = await LiveApi.StartTripAsync();

        var result = await LiveApi.PostPingAsync(trip.TripId, trip.Driver.Token, 91, 79.8612, DateTime.UtcNow);

        LiveApi.Expect(result, HttpStatusCode.BadRequest);
        Assert.NotNull(result.Json["errors"]!["latitude"]);
    }

    [LiveApiFact]
    public async Task L14_07_EmptyBody_Returns400ListingAllFourFields()
    {
        var trip = await LiveApi.StartTripAsync();

        var result = await LiveApi.SendAsync(LiveApi.TrackingUrl, "/api/tracking/locations",
            HttpMethod.Post, token: trip.Driver.Token, rawJson: "{}");

        LiveApi.Expect(result, HttpStatusCode.BadRequest);
        var errors = result.Json["errors"]!.AsObject();
        Assert.Equal(4, errors.Count);
    }

    /// <summary>
    /// EXPLORATORY. A driver's phone in Sri Lanka may send local time without an offset, e.g. "2026-10-12T15:30:00".
    /// The service compares observedAt with UTC, so such a ping can look about 5.5 hours in the future and be rejected
    /// with "Observation time cannot be in the future." The expected behaviour is an accepted ping (201).
    /// If this test fails with 400, report it as a bug (the API contract does not say offsets are mandatory).
    /// </summary>
    [LiveApiFact]
    public async Task L14_08_ObservedAtWithoutOffset_SriLankanLocalTime_IsAccepted()
    {
        var trip = await LiveApi.StartTripAsync();
        var localNoOffset = DateTime.UtcNow.AddHours(5.5).AddSeconds(-5).ToString("yyyy-MM-ddTHH:mm:ss");
        var body = $"{{\"tripId\":\"{trip.TripId}\",\"latitude\":6.9271,\"longitude\":79.8612,\"observedAt\":\"{localNoOffset}\"}}";

        var result = await LiveApi.SendAsync(LiveApi.TrackingUrl, "/api/tracking/locations",
            HttpMethod.Post, token: trip.Driver.Token, rawJson: body);

        LiveApi.Expect(result, HttpStatusCode.Created);
    }

    [LiveApiFact]
    public async Task L14_09_TripNotYetAccepted_Returns409()
    {
        var trip = await LiveApi.StartTripAsync(accepted: false); // status stays Assigned

        var result = await LiveApi.PostPingAsync(trip.TripId, trip.Driver.Token, 6.9271, 79.8612, DateTime.UtcNow);

        LiveApi.Expect(result, HttpStatusCode.Conflict);
    }

    [LiveApiFact]
    public async Task L14_10_UnknownTripId_Returns404()
    {
        var trip = await LiveApi.StartTripAsync();

        var result = await LiveApi.PostPingAsync(Guid.NewGuid().ToString(), trip.Driver.Token, 6.9271, 79.8612, DateTime.UtcNow);

        LiveApi.Expect(result, HttpStatusCode.NotFound);
    }
}
