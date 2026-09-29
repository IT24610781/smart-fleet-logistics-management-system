using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
namespace TripDispatchService.Services;

public class DriverEligibilityClient(IHttpClientFactory clients)
{
    public async Task EnsureActiveAsync(Guid driverId, string authorization, CancellationToken cancellationToken)
    {
        List<DriverAccount> drivers;
        try
        {
            var client = clients.CreateClient("AuthAccountStatus");
            if (client.BaseAddress is null) throw new HttpRequestException("Auth service URL is not configured.");
            using var request = new HttpRequestMessage(HttpMethod.Get, "api/users/drivers");
            request.Headers.Authorization = AuthenticationHeaderValue.Parse(authorization);
            using var response = await client.SendAsync(request, cancellationToken);
            response.EnsureSuccessStatusCode();
            drivers = await response.Content.ReadFromJsonAsync<List<DriverAccount>>(cancellationToken)
                ?? throw new JsonException("Missing Driver list.");
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or FormatException ||
            ex is OperationCanceledException && !cancellationToken.IsCancellationRequested)
        {
            throw new DriverEligibilityUnavailableException("Driver account status could not be verified. No assignment was made.", ex);
        }
        if (!drivers.Any(d => d.Id == driverId && d.IsActive && d.Role == "Driver"))
            throw new InvalidOperationException("The selected Driver does not exist or is inactive.");
    }
    private sealed record DriverAccount(Guid Id, string Role, bool IsActive);
}
public class DriverEligibilityUnavailableException(string message, Exception inner) : Exception(message, inner);
