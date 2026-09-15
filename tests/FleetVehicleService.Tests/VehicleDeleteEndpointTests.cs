using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FleetVehicleService.DTOs;

namespace FleetVehicleService.Tests;

public class VehicleDeleteEndpointTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public VehicleDeleteEndpointTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private HttpClient CreateAuthorizedClient(string role)
    {
        var client = _factory.CreateClient();
        var token = TestJwt.CreateToken(role);
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private async Task<VehicleResponse> CreateTestVehicleAsync(HttpClient adminClient)
    {
        var request = new CreateVehicleRequest
        {
            RegistrationNo = $"DEL-{Guid.NewGuid().ToString()[..8]}",
            Type = "Van",
            Capacity = "6"
        };
        var response = await adminClient.PostAsJsonAsync("/api/vehicles", request);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<VehicleResponse>())!;
    }

    [Fact]
    public async Task DeleteVehicle_ExistingVehicle_Returns204AndRemovesFromList()
    {
        var client = CreateAuthorizedClient("Admin");
        var vehicle = await CreateTestVehicleAsync(client);

        var deleteResponse = await client.DeleteAsync($"/api/vehicles/{vehicle.Id}");
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        var listResponse = await client.GetAsync("/api/vehicles");
        var vehicles = await listResponse.Content.ReadFromJsonAsync<List<VehicleResponse>>();
        Assert.DoesNotContain(vehicles!, v => v.Id == vehicle.Id);
    }

    [Fact]
    public async Task DeleteVehicle_NonExistentId_Returns404()
    {
        var client = CreateAuthorizedClient("Admin");

        var response = await client.DeleteAsync($"/api/vehicles/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task DeleteVehicle_DispatcherRole_Returns403()
    {
        var adminClient = CreateAuthorizedClient("Admin");
        var vehicle = await CreateTestVehicleAsync(adminClient);

        var dispatcherClient = CreateAuthorizedClient("Dispatcher");
        var response = await dispatcherClient.DeleteAsync($"/api/vehicles/{vehicle.Id}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task DeleteVehicle_NoToken_Returns401()
    {
        var adminClient = CreateAuthorizedClient("Admin");
        var vehicle = await CreateTestVehicleAsync(adminClient);

        var anonymousClient = _factory.CreateClient();
        var response = await anonymousClient.DeleteAsync($"/api/vehicles/{vehicle.Id}");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task DeleteVehicle_AlreadyDeleted_Returns404OnSecondAttempt()
    {
        var client = CreateAuthorizedClient("Admin");
        var vehicle = await CreateTestVehicleAsync(client);

        var first = await client.DeleteAsync($"/api/vehicles/{vehicle.Id}");
        Assert.Equal(HttpStatusCode.NoContent, first.StatusCode);

        var second = await client.DeleteAsync($"/api/vehicles/{vehicle.Id}");
        Assert.Equal(HttpStatusCode.NotFound, second.StatusCode);
    }

    // KNOWN GAP — documented, not a passing/failing assertion of
    // "correct" behavior. DeleteVehicleAsync only calls
    // ITripEligibilityChecker.HasActiveOrUpcomingTripsAsync, which
    // is currently StubTripEligibilityChecker (always returns false).
    // It never inspects the vehicle's OWN `status` column. This test
    // documents that a vehicle marked "On Trip" can be deleted TODAY.
    // Recommendation raised to the team: add an explicit status check
    // independent of the real Trip service integration (deferred to
    // a later sprint), OR explicitly accept this as a documented
    // limitation until Trip integration lands.
    // See: QA finding, Sprint 2 — raise as a new Jira ticket.

    [Fact]
    public async Task DeleteVehicle_VehicleStatusIsOnTrip_CurrentlySucceeds_KnownGap()
    {
        var client = CreateAuthorizedClient("Admin");
        var vehicle = await CreateTestVehicleAsync(client);

        var setOnTrip = new UpdateVehicleRequest
        {
            Type = vehicle.Type,
            Capacity = vehicle.Capacity.ToString(),
            Status = "On Trip"
        };
        var updateResponse = await client.PutAsJsonAsync($"/api/vehicles/{vehicle.Id}", setOnTrip);
        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);

        var deleteResponse = await client.DeleteAsync($"/api/vehicles/{vehicle.Id}");


        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);
    }
}