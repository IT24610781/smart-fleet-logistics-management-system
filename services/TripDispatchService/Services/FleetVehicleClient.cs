using System.Net.Http.Headers;
using System.Net.Http.Json;
using TripDispatchService.DTOs;

namespace TripDispatchService.Services;

public class FleetVehicleClient : IFleetVehicleClient
{
    private readonly HttpClient _httpClient;

    public FleetVehicleClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<List<FleetVehicleDto>> GetVehiclesAsync(string? authorizationHeader)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/vehicles");

        if (!string.IsNullOrWhiteSpace(authorizationHeader) &&
            AuthenticationHeaderValue.TryParse(authorizationHeader, out var auth))
        {
            request.Headers.Authorization = auth;
        }

        using var response = await _httpClient.SendAsync(request);

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"Fleet service returned {(int)response.StatusCode} while listing vehicles.");
        }

        var vehicles = await response.Content.ReadFromJsonAsync<List<FleetVehicleDto>>();

        return vehicles ?? new List<FleetVehicleDto>();
    }
}