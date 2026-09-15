using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FleetVehicleService.DTOs;

namespace FleetVehicleService.Tests;

public class VehicleCreateEndpointTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public VehicleCreateEndpointTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private HttpClient CreateAuthorizedClient(string role = "Admin")
    {
        var client = _factory.CreateClient();
        var token = TestJwt.CreateToken(role);
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    [Fact]
    public async Task CreateVehicle_ValidRequest_Returns201WithVehicleDetails()
    {
        var client = CreateAuthorizedClient("Admin");
        var request = new CreateVehicleRequest
        {
            RegistrationNo = $"TEST-{Guid.NewGuid().ToString()[..8]}",
            Type = "Van",
            Capacity = "8"
        };

        var response = await client.PostAsJsonAsync("/api/vehicles", request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<VehicleResponse>();
        Assert.NotNull(body);
        Assert.Equal(request.RegistrationNo, body!.RegistrationNo);
        Assert.Equal("Van", body.Type);
        Assert.Equal(8, body.Capacity);
        Assert.Equal("Available", body.Status);
    }

    [Fact]
    public async Task CreateVehicle_DuplicateRegistrationNo_Returns409()
    {
        var client = CreateAuthorizedClient("Admin");
        var regNo = $"DUPLICATE-{Guid.NewGuid().ToString()[..8]}";

        var first = new CreateVehicleRequest
        {
            RegistrationNo = regNo,
            Type = "Truck",
            Capacity = "10"
        };
        var firstResponse = await client.PostAsJsonAsync("/api/vehicles", first);
        Assert.Equal(HttpStatusCode.Created, firstResponse.StatusCode);

        var second = new CreateVehicleRequest
        {
            RegistrationNo = regNo,
            Type = "Bike",
            Capacity = "2"
        };
        var secondResponse = await client.PostAsJsonAsync("/api/vehicles", second);

        Assert.Equal(HttpStatusCode.Conflict, secondResponse.StatusCode);
    }

    [Fact]
    public async Task CreateVehicle_MissingRegistrationNo_Returns400WithFieldError()
    {
        var client = CreateAuthorizedClient("Admin");
        var request = new CreateVehicleRequest
        {
            RegistrationNo = "",
            Type = "Van",
            Capacity = "5"
        };

        var response = await client.PostAsJsonAsync("/api/vehicles", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("-5")]
    [InlineData("0")]
    [InlineData("")]
    public async Task CreateVehicle_InvalidCapacity_Returns400(string invalidCapacity)
    {
        var client = CreateAuthorizedClient("Admin");
        var request = new CreateVehicleRequest
        {
            RegistrationNo = $"CAP-{Guid.NewGuid().ToString()[..8]}",
            Type = "Van",
            Capacity = invalidCapacity
        };

        var response = await client.PostAsJsonAsync("/api/vehicles", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateVehicle_DispatcherRole_Returns403()
    {
        var client = CreateAuthorizedClient("Dispatcher");
        var request = new CreateVehicleRequest
        {
            RegistrationNo = $"FORBIDDEN-{Guid.NewGuid().ToString()[..8]}",
            Type = "Van",
            Capacity = "5"
        };

        var response = await client.PostAsJsonAsync("/api/vehicles", request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task CreateVehicle_NoToken_Returns401()
    {
        var client = _factory.CreateClient();
        var request = new CreateVehicleRequest
        {
            RegistrationNo = $"NOAUTH-{Guid.NewGuid().ToString()[..8]}",
            Type = "Van",
            Capacity = "5"
        };

        var response = await client.PostAsJsonAsync("/api/vehicles", request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}