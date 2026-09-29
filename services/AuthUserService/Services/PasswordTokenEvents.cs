using System.Security.Claims;
using AuthUserService.Data;
using Microsoft.AspNetCore.Authentication.JwtBearer;

namespace AuthUserService.Services;

public static class PasswordTokenEvents
{
    public static JwtBearerEvents Create()
    {
        var events = DriverTokenEvents.Create();
        var driverValidation = events.OnTokenValidated;
        events.OnTokenValidated = async context =>
        {
            await driverValidation(context);
            if (context.Result?.Failure is not null) return;
            var subject = context.Principal?.FindFirstValue(ClaimTypes.NameIdentifier) ?? context.Principal?.FindFirstValue("sub");
            if (!Guid.TryParse(subject, out var id)) { context.Fail("Invalid account identity."); return; }
            try
            {
                var services = context.HttpContext.RequestServices;
                var stamp = context.Principal?.FindFirstValue("pwdv");
                if (stamp is null)
                {
                    // Pre-upgrade JWTs stop working after the first verified password change.
                    if (await services.GetRequiredService<IPasswordChallengeRepository>().HasChangedAsync(id, context.HttpContext.RequestAborted))
                        context.Fail("Password changed. Sign in again.");
                    return;
                }
                var user = await services.GetRequiredService<IUserAccountRepository>().GetByIdAsync(id);
                var key = services.GetRequiredService<IConfiguration>()["Jwt:Key"]!;
                if (user is null || !user.IsActive || !PasswordSecurity.Matches(stamp, PasswordSecurity.Stamp(user.PasswordHash, key)))
                    context.Fail("Account session has ended. Sign in again.");
            }
            catch (Exception ex) when (ex is MySqlConnector.MySqlException or TimeoutException)
            {
                context.HttpContext.Items["DriverStatusUnavailable"] = true;
                context.Fail("Account status could not be verified.");
            }
        };
        return events;
    }
}
