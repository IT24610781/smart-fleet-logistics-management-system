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

    public async Task SetVehicleStatusAsync(Guid vehicleId, string status, string? authorizationHeader)
    {
        using var request = new HttpRequestMessage(HttpMethod.Patch, $"/api/vehicles/{vehicleId}/status")
        {
            Content = JsonContent.Create(new { status })
        };

        if (!string.IsNullOrWhiteSpace(authorizationHeader) &&
            AuthenticationHeaderValue.TryParse(authorizationHeader, out var auth))
        {
            request.Headers.Authorization = auth;
        }

        using var response = await _httpClient.SendAsync(request);

        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var body = await response.Content.ReadAsStringAsync();

        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            throw new NotFoundException($"No vehicle found with id '{vehicleId}'.");
        }

        if (response.StatusCode == System.Net.HttpStatusCode.Conflict)
        {
            throw new InvalidOperationException(
                ExtractMessage(body) ?? $"Vehicle '{vehicleId}' is not available for this status change.");
        }

        throw new InvalidOperationException(
            $"Fleet service returned {(int)response.StatusCode} while updating vehicle status.");
    }

    private static string? ExtractMessage(string jsonBody)
    {
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(jsonBody);
            return doc.RootElement.TryGetProperty("message", out var m) ? m.GetString() : null;
        }
        catch
        {
            return null;
        }
    }
}