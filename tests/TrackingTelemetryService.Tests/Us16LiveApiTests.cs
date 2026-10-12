using System.Net;

namespace TrackingTelemetryService.Tests;

/// <summary>
/// SCRUM-153 / US-16: "System stores trip location history", checked against the real database.
/// Opt-in: see LiveApiHarness.cs for the environment variables.
///   dotnet test tests/TrackingTelemetryService.Tests --filter "Story=US-16&amp;Category=LiveApi"
/// Needs LiveApiHarness.cs from the US-14 branch (merge that branch first).
///
/// L16_02 is an exploratory probe that is expected to FAIL against the current code: the history query is
/// "ORDER BY observed_at ASC LIMIT n", so a limit smaller than the number of pings returns the OLDEST pings and
/// silently drops the newest. The in-memory unit tests cannot see this because their fake repository copies the same logic.
/// </summary>
[Trait("Story", "US-16")]
[Trait("Category", "LiveApi")]
public class Us16LiveApiTests
{
    // Latitude doubles as a marker: 1.1 = oldest ping, 2.2 = middle, 3.3 = newest.
    private static async Task PostThreePingsOutOfOrderAsync(LiveTrip trip)
    {
        var now = DateTime.UtcNow;
        LiveApi.Expect(await LiveApi.PostPingAsync(trip.TripId, trip.Driver.Token, 3.3, 80.0, now.AddMinutes(-1)), HttpStatusCode.Created);
        LiveApi.Expect(await LiveApi.PostPingAsync(trip.TripId, trip.Driver.Token, 1.1, 80.0, now.AddMinutes(-3)), HttpStatusCode.Created);
        LiveApi.Expect(await LiveApi.PostPingAsync(trip.TripId, trip.Driver.Token, 2.2, 80.0, now.AddMinutes(-2)), HttpStatusCode.Created);
    }

    [LiveApiFact]
    public async Task L16_01_History_IsReturnedOldestToNewestEvenWhenPostedOutOfOrder()
    {
        var trip = await LiveApi.StartTripAsync();
        await PostThreePingsOutOfOrderAsync(trip);

        var result = await LiveApi.GetHistoryAsync(trip.TripId, trip.DispatcherToken);

        LiveApi.Expect(result, HttpStatusCode.OK);
        var latitudes = result.Json.AsArray().Select(n => n!["latitude"]!.GetValue<decimal>()).ToList();
        Assert.Equal(new[] { 1.1m, 2.2m, 3.3m }, latitudes);
    }

    [LiveApiFact]
    public async Task L16_02_SmallLimit_StillIncludesTheNewestPing()
    {
        var trip = await LiveApi.StartTripAsync();
        await PostThreePingsOutOfOrderAsync(trip);

        var result = await LiveApi.GetHistoryAsync(trip.TripId, trip.DispatcherToken, "?limit=2");

        LiveApi.Expect(result, HttpStatusCode.OK);
        var latitudes = result.Json.AsArray().Select(n => n!["latitude"]!.GetValue<decimal>()).ToList();
        Assert.Equal(2, latitudes.Count);
        Assert.Contains(3.3m, latitudes); // the newest ping must not be the one that gets dropped
    }

    [LiveApiFact]
    public async Task L16_03_AccessRules_OwnDriverAndDispatcherAllowed_OtherDriverGets404()
    {
        var trip = await LiveApi.StartTripAsync();
        await PostThreePingsOutOfOrderAsync(trip);
        var otherDriver = await LiveApi.CreateDriverAsync(await LiveApi.AdminTokenAsync());

        LiveApi.Expect(await LiveApi.GetHistoryAsync(trip.TripId, trip.Driver.Token), HttpStatusCode.OK);
        LiveApi.Expect(await LiveApi.GetHistoryAsync(trip.TripId, trip.DispatcherToken), HttpStatusCode.OK);
        LiveApi.Expect(await LiveApi.GetHistoryAsync(trip.TripId, otherDriver.Token), HttpStatusCode.NotFound);
    }

    [LiveApiFact]
    public async Task L16_04_LimitBoundaries_0And1001AreRejected_1And1000AreAccepted()
    {
        var trip = await LiveApi.StartTripAsync();

        LiveApi.Expect(await LiveApi.GetHistoryAsync(trip.TripId, trip.DispatcherToken, "?limit=0"), HttpStatusCode.BadRequest);
        LiveApi.Expect(await LiveApi.GetHistoryAsync(trip.TripId, trip.DispatcherToken, "?limit=1001"), HttpStatusCode.BadRequest);
        LiveApi.Expect(await LiveApi.GetHistoryAsync(trip.TripId, trip.DispatcherToken, "?limit=1"), HttpStatusCode.OK);
        LiveApi.Expect(await LiveApi.GetHistoryAsync(trip.TripId, trip.DispatcherToken, "?limit=1000"), HttpStatusCode.OK);
    }

    [LiveApiFact]
    public async Task L16_05_TripWithNoPings_ReturnsEmptyListNotAnError()
    {
        var trip = await LiveApi.StartTripAsync();

        var result = await LiveApi.GetHistoryAsync(trip.TripId, trip.DispatcherToken);

        LiveApi.Expect(result, HttpStatusCode.OK);
        Assert.Empty(result.Json.AsArray());
    }

    [LiveApiFact]
    public async Task L16_06_UnknownTrip_Returns404()
    {
        var trip = await LiveApi.StartTripAsync();

        var result = await LiveApi.GetHistoryAsync(Guid.NewGuid().ToString(), trip.DispatcherToken);

        LiveApi.Expect(result, HttpStatusCode.NotFound);
    }

    [LiveApiFact]
    public async Task L16_07_NoToken_Returns401()
    {
        var trip = await LiveApi.StartTripAsync();

        var result = await LiveApi.GetHistoryAsync(trip.TripId, null);

        LiveApi.Expect(result, HttpStatusCode.Unauthorized);
    }

    [LiveApiFact]
    public async Task L16_08_NonNumericLimit_IsRejected()
    {
        var trip = await LiveApi.StartTripAsync();

        var result = await LiveApi.GetHistoryAsync(trip.TripId, trip.DispatcherToken, "?limit=abc");

        LiveApi.Expect(result, HttpStatusCode.BadRequest);
    }
}
