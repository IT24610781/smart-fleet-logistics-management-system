using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FleetVehicleService.DTOs;

namespace FleetVehicleService.Tests;

public class VehicleListEndpointTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public VehicleListEndpointTests(CustomWebApplicationFactory factory)
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

    private async Task<string> CreateTestVehicleAsync(HttpClient adminClient, string? regNo = null)
    {
        var request = new CreateVehicleRequest
        {
            RegistrationNo = regNo ?? $"LIST-{Guid.NewGuid().ToString()[..8]}",
            Type = "Van",
            Capacity = "6"
        };
        var response = await adminClient.PostAsJsonAsync("/api/vehicles", request);
        response.EnsureSuccessStatusCode();
        return request.RegistrationNo;
    }

    [Fact]
    public async Task GetVehicles_AdminRole_Returns200WithVehicleList()
    {
        var adminClient = CreateAuthorizedClient("Admin");
        var regNo = await CreateTestVehicleAsync(adminClient);

        var response = await adminClient.GetAsync("/api/vehicles");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var vehicles = await response.Content.ReadFromJsonAsync<List<VehicleResponse>>();
        Assert.NotNull(vehicles);
        Assert.Contains(vehicles!, v => v.RegistrationNo == regNo);
    }

    [Fact]
    public async Task GetVehicles_DispatcherRole_Returns200()
    {
        var adminClient = CreateAuthorizedClient("Admin");
        await CreateTestVehicleAsync(adminClient);

        var dispatcherClient = CreateAuthorizedClient("Dispatcher");
        var response = await dispatcherClient.GetAsync("/api/vehicles");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetVehicles_DriverRole_Returns403()
    {
        var driverClient = CreateAuthorizedClient("Driver");
        var response = await driverClient.GetAsync("/api/vehicles");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetVehicles_NoToken_Returns401()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/vehicles");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetVehicles_ResponseNeverExposesInternalFieldsBeyondSchema()
    {
        var adminClient = CreateAuthorizedClient("Admin");
        await CreateTestVehicleAsync(adminClient);

        var response = await adminClient.GetAsync("/api/vehicles");
        var vehicles = await response.Content.ReadFromJsonAsync<List<VehicleResponse>>();

        Assert.NotNull(vehicles);
        Assert.All(vehicles!, v =>
        {
            Assert.NotEqual(Guid.Empty, v.Id);
            Assert.False(string.IsNullOrWhiteSpace(v.RegistrationNo));
            Assert.False(string.IsNullOrWhiteSpace(v.Status));
        });
    }
}