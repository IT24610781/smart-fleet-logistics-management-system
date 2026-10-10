using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace TripDispatchService.Services;

public class DriverDirectoryClient(IHttpClientFactory clients) : IDriverDirectoryClient
{
    public async Task<List<DriverSummary>> GetDriversAsync(
        string? authorizationHeader, CancellationToken cancellationToken)
    {
        try
        {
            var client = clients.CreateClient("AuthAccountStatus");
            if (client.BaseAddress is null) throw new HttpRequestException("Auth service URL is not configured.");

            using var request = new HttpRequestMessage(HttpMethod.Get, "api/users/drivers");
            if (!string.IsNullOrEmpty(authorizationHeader))
            {
                request.Headers.Authorization = AuthenticationHeaderValue.Parse(authorizationHeader);
            }

            using var response = await client.SendAsync(request, cancellationToken);
            response.EnsureSuccessStatusCode();

            var drivers = await response.Content.ReadFromJsonAsync<List<DriverAccountDto>>(cancellationToken)
                ?? throw new JsonException("Missing Driver list.");

            return drivers
                .Where(d => d.Role == "Driver")
                .Select(d => new DriverSummary(d.Id, d.FirstName, d.LastName, d.IsActive))
                .ToList();
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or FormatException ||
            (ex is OperationCanceledException && !cancellationToken.IsCancellationRequested))
        {
            throw new DriverDirectoryUnavailableException(
                "Driver accounts could not be retrieved. The performance report could not be generated.", ex);
        }
    }

    private sealed record DriverAccountDto(Guid Id, string FirstName, string LastName, string Role, bool IsActive);
}

public class DriverDirectoryUnavailableException(string message, Exception inner) : Exception(message, inner);