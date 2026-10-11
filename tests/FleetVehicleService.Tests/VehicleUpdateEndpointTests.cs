using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FleetVehicleService.DTOs;

namespace FleetVehicleService.Tests;

public class VehicleUpdateEndpointTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public VehicleUpdateEndpointTests(CustomWebApplicationFactory factory)
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
            RegistrationNo = $"UPD-{Guid.NewGuid().ToString()[..8]}",
            Type = "Van",
            Capacity = "6"
        };
        var response = await adminClient.PostAsJsonAsync("/api/vehicles", request);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<VehicleResponse>())!;
    }

    [Fact]
    public async Task UpdateVehicle_ValidRequest_Returns200WithUpdatedDetails()
    {
        var client = CreateAuthorizedClient("Admin");
        var vehicle = await CreateTestVehicleAsync(client);

        var update = new UpdateVehicleRequest
        {
            RegistrationNo = vehicle.RegistrationNo,
            Type = "Truck",
            Capacity = "12",
            Status = "Available"
        };

        var response = await client.PutAsJsonAsync($"/api/vehicles/{vehicle.Id}", update);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<VehicleResponse>();
        Assert.Equal("Truck", body!.Type);
        Assert.Equal("12", body.Capacity);
    }

    [Fact]
    public async Task UpdateVehicle_NonExistentId_Returns404()
    {
        var client = CreateAuthorizedClient("Admin");
        var update = new UpdateVehicleRequest
        {
            Type = "Truck",
            Capacity = "12",
            Status = "Available"
        };

        var response = await client.PutAsJsonAsync($"/api/vehicles/{Guid.NewGuid()}", update);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task UpdateVehicle_InvalidStatus_Returns400()
    {
        var client = CreateAuthorizedClient("Admin");
        var vehicle = await CreateTestVehicleAsync(client);

        var update = new UpdateVehicleRequest
        {
            Type = "Truck",
            Capacity = "12",
            Status = "NotARealStatus"
        };

        var response = await client.PutAsJsonAsync($"/api/vehicles/{vehicle.Id}", update);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // SCRUM-86 (same root cause): type is VARCHAR(50), registration_no VARCHAR(20).
    [Fact]
    public async Task UpdateVehicle_TypeTooLong_Returns400()
    {
        var client = CreateAuthorizedClient("Admin");
        var vehicle = await CreateTestVehicleAsync(client);

        var update = new UpdateVehicleRequest
        {
            RegistrationNo = vehicle.RegistrationNo,
            Type = new string('T', 51),
            Capacity = "12",
            Status = "Available"
        };

        var response = await client.PutAsJsonAsync($"/api/vehicles/{vehicle.Id}", update);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task UpdateVehicle_RegistrationNoTooLong_Returns400()
    {
        var client = CreateAuthorizedClient("Admin");
        var vehicle = await CreateTestVehicleAsync(client);

        var update = new UpdateVehicleRequest
        {
            RegistrationNo = new string('X', 41),
            Type = "Truck",
            Capacity = "12",
            Status = "Available"
        };

        var response = await client.PutAsJsonAsync($"/api/vehicles/{vehicle.Id}", update);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task UpdateVehicle_CapacityWithUnit_Returns200AndStoresNormalisedText()
    {
        var client = CreateAuthorizedClient("Admin");
        var vehicle = await CreateTestVehicleAsync(client);

        var update = new UpdateVehicleRequest
        {
            RegistrationNo = vehicle.RegistrationNo,
            Type = "Truck",
            Capacity = "20kg",
            Status = "Available"
        };

        var response = await client.PutAsJsonAsync($"/api/vehicles/{vehicle.Id}", update);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<VehicleResponse>();
        Assert.Equal("20 kg", body!.Capacity);
    }

    [Fact]
    public async Task UpdateVehicle_InvalidCapacity_Returns400()
    {
        var client = CreateAuthorizedClient("Admin");
        var vehicle = await CreateTestVehicleAsync(client);

        var update = new UpdateVehicleRequest
        {
            Type = "Truck",
            Capacity = "-3",
            Status = "Available"
        };

        var response = await client.PutAsJsonAsync($"/api/vehicles/{vehicle.Id}", update);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task UpdateVehicle_DispatcherRole_Returns403()
    {
        var adminClient = CreateAuthorizedClient("Admin");
        var vehicle = await CreateTestVehicleAsync(adminClient);

        var dispatcherClient = CreateAuthorizedClient("Dispatcher");
        var update = new UpdateVehicleRequest
        {
            Type = "Truck",
            Capacity = "12",
            Status = "Available"
        };

        var response = await dispatcherClient.PutAsJsonAsync($"/api/vehicles/{vehicle.Id}", update);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task UpdateVehicle_StatusChange_PublishesEventViaFakePublisher()
    {
        var client = CreateAuthorizedClient("Admin");
        var vehicle = await CreateTestVehicleAsync(client);

        _factory.EventPublisher.Published.Clear();

        var update = new UpdateVehicleRequest
        {
            RegistrationNo = vehicle.RegistrationNo,
            Type = vehicle.Type,
            Capacity = vehicle.Capacity.ToString(),
            Status = "Under Maintenance"
        };

        var response = await client.PutAsJsonAsync($"/api/vehicles/{vehicle.Id}", update);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        Assert.Contains(_factory.EventPublisher.Published,
            e => e.VehicleId == vehicle.Id
                 && e.PreviousStatus == "Available"
                 && e.NewStatus == "Under Maintenance");
    }

    [Fact]
    public async Task UpdateVehicle_SameStatus_DoesNotPublishEvent()
    {
        var client = CreateAuthorizedClient("Admin");
        var vehicle = await CreateTestVehicleAsync(client);

        _factory.EventPublisher.Published.Clear();

        var update = new UpdateVehicleRequest
        {
            RegistrationNo = vehicle.RegistrationNo,
            Type = vehicle.Type,
            Capacity = vehicle.Capacity.ToString(),
            Status = vehicle.Status
        };

        var response = await client.PutAsJsonAsync($"/api/vehicles/{vehicle.Id}", update);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        Assert.DoesNotContain(_factory.EventPublisher.Published,
            e => e.VehicleId == vehicle.Id);
    }
}