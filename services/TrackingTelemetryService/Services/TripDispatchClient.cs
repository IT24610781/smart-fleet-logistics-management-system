using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace TrackingTelemetryService.Services;

public class TripDispatchClient(IHttpClientFactory clients) : ITripDispatchClient
{
    public async Task<TripSnapshot?> GetTripAsync(
        Guid tripId, string? authorizationHeader, CancellationToken cancellationToken)
    {
        try
        {
            var client = clients.CreateClient("TripDispatch");
            using var request = new HttpRequestMessage(HttpMethod.Get, $"api/trips/{tripId}");
            if (!string.IsNullOrEmpty(authorizationHeader))
            {
                request.Headers.Authorization = AuthenticationHeaderValue.Parse(authorizationHeader);
            }

            using var response = await client.SendAsync(request, cancellationToken);

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return null;
            }

            response.EnsureSuccessStatusCode();

            return await response.Content.ReadFromJsonAsync<TripSnapshot>(cancellationToken);
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or FormatException ||
            (ex is OperationCanceledException && !cancellationToken.IsCancellationRequested))
        {
            throw new TripServiceUnavailableException(
                "Trip status could not be verified. No location update was recorded.", ex);
        }
    }
}