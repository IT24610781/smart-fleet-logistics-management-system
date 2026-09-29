using System.Net.Http.Headers;
using System.Text.Json;
using AuthUserService.Data;

namespace AuthUserService.Services;

public class DriverDeactivationService(IUserAccountRepository users, HttpClient trips)
{
    public async Task DeactivateAsync(Guid id, string authorization, CancellationToken cancellationToken)
    {
        var user = await users.GetByIdAsync(id);
        if (user is null || user.Role != "Driver")
            throw new KeyNotFoundException("Driver account was not found.");
        if (!user.IsActive) return;
        try
        {
            if (trips.BaseAddress is null) throw new HttpRequestException("Trip service URL is not configured.");
            using var request = new HttpRequestMessage(HttpMethod.Post, $"api/trips/drivers/{id}/deactivation-check");
            request.Headers.Authorization = AuthenticationHeaderValue.Parse(authorization);
            using var response = await trips.SendAsync(request, cancellationToken);
            if (response.StatusCode == System.Net.HttpStatusCode.Conflict)
                throw new InvalidOperationException("This Driver has an assigned or active trip. Complete or resolve the assignment before deactivating the account.");
            // Only the documented success response permits an account change.
            if (response.StatusCode != System.Net.HttpStatusCode.NoContent)
                throw new HttpRequestException("Trip deactivation check did not succeed.");
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or FormatException ||
                                  ex is OperationCanceledException && !cancellationToken.IsCancellationRequested)
        {
            throw new DriverDependencyException("Trip status could not be verified. The Driver has not been deactivated. Try again when Trip service is available.", ex);
        }
        await users.DeactivateDriverAsync(id);
    }
}

public class DriverDependencyException(string message, Exception inner) : Exception(message, inner);
