using System.Security.Claims;
using AuthUserService.Data;
using Microsoft.AspNetCore.Authentication.JwtBearer;

namespace AuthUserService.Services;

public static class DriverTokenEvents
{
    public static JwtBearerEvents Create() => new()
    {
        OnTokenValidated = async context =>
        {
            if (context.Principal?.IsInRole("Driver") != true) return;
            var subject = context.Principal.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? context.Principal.FindFirstValue("sub");
            if (!Guid.TryParse(subject, out var id))
            {
                context.Fail("Invalid Driver identity.");
                return;
            }
            try
            {
                var user = await context.HttpContext.RequestServices
                    .GetRequiredService<IUserAccountRepository>().GetByIdAsync(id);
                if (user is null || !user.IsActive || user.Role != "Driver")
                    context.Fail("Driver account is inactive or unavailable.");
            }
            catch (Exception ex) when (ex is MySqlConnector.MySqlException or TimeoutException)
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
}
