using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication.JwtBearer;

namespace FleetVehicleService.Services;

public static class AccountTokenEvents
{
    public static JwtBearerEvents Create() => new()
    {
        OnTokenValidated = async context =>
        {
            try
            {
                var client = context.HttpContext.RequestServices.GetRequiredService<IHttpClientFactory>().CreateClient("AuthAccountStatus");
                if (client.BaseAddress is null) throw new HttpRequestException("Auth service URL is not configured.");
                using var request = new HttpRequestMessage(HttpMethod.Get, "api/users/me");
                request.Headers.Authorization = AuthenticationHeaderValue.Parse(context.Request.Headers.Authorization.ToString());
                using var response = await client.SendAsync(request, context.HttpContext.RequestAborted);
                if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden or HttpStatusCode.NotFound)
                {
                    context.Fail("Account session is inactive or unavailable.");
                    return;
                }
                response.EnsureSuccessStatusCode();
                var user = await response.Content.ReadFromJsonAsync<AccountProfile>(context.HttpContext.RequestAborted);
                var subject = context.Principal?.FindFirstValue(ClaimTypes.NameIdentifier) ?? context.Principal?.FindFirstValue("sub");
                if (user is null || !user.IsActive || user.Role != context.Principal?.FindFirstValue(ClaimTypes.Role) || !Guid.TryParse(subject, out var id) || user.Id != id)
                    context.Fail("Account session is inactive or unavailable.");
            }
            catch (Exception ex) when (ex is HttpRequestException or JsonException or FormatException ||
                ex is OperationCanceledException && !context.HttpContext.RequestAborted.IsCancellationRequested)
            {
                context.HttpContext.Items["DriverStatusUnavailable"] = true;
                context.Fail("Account status could not be verified.");
            }
        },
        OnChallenge = async context =>
        {
            if (!context.HttpContext.Items.ContainsKey("DriverStatusUnavailable")) return;
            context.HandleResponse();
            context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            await context.Response.WriteAsJsonAsync(new { message = "Account status could not be verified. Try again shortly." });
        }
    };
    private sealed record AccountProfile(Guid Id, string Role, bool IsActive);
}
