using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using AuthUserService.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace AuthUserService.Controllers;

[ApiController]
[Route("api/auth/password")]
public sealed class PasswordController(PasswordService passwords) : ControllerBase
{
    public sealed record ResetRequest([Required, EmailAddress, MaxLength(254)] string Email);
    public sealed record ChangeRequest([Required, MaxLength(256)] string CurrentPassword);
    public sealed record CompleteRequest(Guid ChallengeId, [Required, MaxLength(6)] string Code,
        [Required, MaxLength(128)] string NewPassword, [Required, MaxLength(128)] string ConfirmPassword);

    [AllowAnonymous, HttpPost("forgot"), EnableRateLimiting("password-request")]
    public Task<IActionResult> Forgot(ResetRequest request, CancellationToken ct) => Execute(async () =>
    {
        var timer = System.Diagnostics.Stopwatch.StartNew();
        var result = await passwords.RequestAsync(request.Email, null, null, ct);
        var remaining = TimeSpan.FromMilliseconds(500) - timer.Elapsed;
        if (remaining > TimeSpan.Zero) await Task.Delay(remaining, ct);
        return Ok(result);
    });

    [Authorize, HttpPost("change/request"), EnableRateLimiting("password-request")]
    public Task<IActionResult> Change(ChangeRequest request, CancellationToken ct) => Execute(async () =>
        Ok(await passwords.RequestAsync("", Subject(), request.CurrentPassword, ct)));

    [AllowAnonymous, HttpPost("reset"), EnableRateLimiting("password-verify")]
    public Task<IActionResult> Reset(CompleteRequest request, CancellationToken ct) => Complete(request, null, ct);

    [Authorize, HttpPost("change/confirm"), EnableRateLimiting("password-verify")]
    public Task<IActionResult> Confirm(CompleteRequest request, CancellationToken ct) => Complete(request, Subject(), ct);

    private Guid Subject() => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub")!);
    private Task<IActionResult> Complete(CompleteRequest request, Guid? userId, CancellationToken ct) => Execute(async () =>
    {
        await passwords.CompleteAsync(request.ChallengeId, userId, request.Code, request.NewPassword, request.ConfirmPassword, ct);
        return Ok(new { message = "Password updated. Sign in again with your new password." });
    });
    private async Task<IActionResult> Execute(Func<Task<IActionResult>> operation)
    {
        try { return await operation(); }
        catch (ArgumentException ex) { return BadRequest(new { message = ex.Message }); }
        catch (PasswordCooldownException) { return StatusCode(429, new { message = "Wait at least 60 seconds before requesting another code. Up to five codes can be requested per hour." }); }
        catch (PasswordEmailUnavailableException) { return StatusCode(503, new { message = "Email verification is temporarily unavailable. Please try again later or contact your administrator." }); }
    }
}
