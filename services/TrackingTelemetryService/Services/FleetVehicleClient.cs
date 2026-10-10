using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace TrackingTelemetryService.Services;

public class FleetVehicleClient(IHttpClientFactory clients) : IFleetVehicleClient
{
    public async Task<List<FleetVehicleSnapshot>> GetAllVehiclesAsync(
        string? authorizationHeader, CancellationToken cancellationToken)
    {
        try
        {
            var client = clients.CreateClient("FleetVehicle");
            using var request = new HttpRequestMessage(HttpMethod.Get, "api/vehicles");
            if (!string.IsNullOrEmpty(authorizationHeader))
            {
                request.Headers.Authorization = AuthenticationHeaderValue.Parse(authorizationHeader);
            }

            using var response = await client.SendAsync(request, cancellationToken);
            response.EnsureSuccessStatusCode();

            var vehicles = await response.Content.ReadFromJsonAsync<List<FleetVehicleSnapshot>>(cancellationToken);
            return vehicles ?? [];
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or FormatException ||
            (ex is OperationCanceledException && !cancellationToken.IsCancellationRequested))
        {
            throw new FleetServiceUnavailableException(
                "Vehicle data could not be retrieved. The utilization report could not be generated.", ex);
        }
    }
}