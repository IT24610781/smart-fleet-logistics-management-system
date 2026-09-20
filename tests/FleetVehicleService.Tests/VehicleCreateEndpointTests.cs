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
        Assert.Equal("8", body.Capacity);
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

    // SCRUM-86: registration_no is VARCHAR(20). Oversized input used to reach
    // MySQL and surface as a raw 500 with a stack trace.
    [Fact]
    public async Task CreateVehicle_RegistrationNoTooLong_Returns400WithFieldError()
    {
        var client = CreateAuthorizedClient("Admin");
        var request = new CreateVehicleRequest
        {
            RegistrationNo = new string('X', 41),
            Type = "Van",
            Capacity = "5"
        };

        var response = await client.PostAsJsonAsync("/api/vehicles", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("registrationNo", body);
        Assert.DoesNotContain("MySqlException", body);
    }

    [Fact]
    public async Task CreateVehicle_RegistrationNoExactly20Chars_Returns201()
    {
        var client = CreateAuthorizedClient("Admin");
        var request = new CreateVehicleRequest
        {
            RegistrationNo = Guid.NewGuid().ToString("N")[..20],
            Type = "Van",
            Capacity = "5"
        };

        var response = await client.PostAsJsonAsync("/api/vehicles", request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task CreateVehicle_TypeTooLong_Returns400WithFieldError()
    {
        var client = CreateAuthorizedClient("Admin");
        var request = new CreateVehicleRequest
        {
            RegistrationNo = $"TYP-{Guid.NewGuid().ToString()[..8]}",
            Type = new string('T', 51),
            Capacity = "5"
        };

        var response = await client.PostAsJsonAsync("/api/vehicles", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("type", body);
    }

    // Capacity accepts a number with an optional unit and is stored in one
    // consistent format ("500kg" -> "500 kg").
    [Theory]
    [InlineData("1 unit", "1 unit")]
    [InlineData("1 kg", "1 kg")]
    [InlineData("8 persons", "8 persons")]
    [InlineData("500kg", "500 kg")]
    [InlineData("2.5 tons", "2.5 tons")]
    public async Task CreateVehicle_CapacityWithUnit_Returns201AndStoresNormalisedText(
        string capacity, string expectedStored)
    {
        var client = CreateAuthorizedClient("Admin");
        var request = new CreateVehicleRequest
        {
            RegistrationNo = $"CU-{Guid.NewGuid().ToString("N")[..10]}",
            Type = "Van",
            Capacity = capacity
        };

        var response = await client.PostAsJsonAsync("/api/vehicles", request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<VehicleResponse>();
        Assert.Equal(expectedStored, body!.Capacity);
    }

    [Theory]
    [InlineData("kg")]
    [InlineData("5 @#")]
    [InlineData("0 kg")]
    [InlineData("-3 kg")]
    [InlineData("1000000000000 kg")]
    [InlineData("5 abcdefghijklmnopqrstuvwxyz")]
    public async Task CreateVehicle_InvalidCapacityFormat_Returns400(string invalidCapacity)
    {
        var client = CreateAuthorizedClient("Admin");
        var request = new CreateVehicleRequest
        {
            RegistrationNo = $"CF-{Guid.NewGuid().ToString("N")[..10]}",
            Type = "Van",
            Capacity = invalidCapacity
        };

        var response = await client.PostAsJsonAsync("/api/vehicles", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("capacity", await response.Content.ReadAsStringAsync());
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