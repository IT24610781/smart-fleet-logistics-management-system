using AuthUserService.DTOs;
using AuthUserService.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AuthUserService.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly AuthService _authService;
    private readonly UserService _userService;

    public AuthController(AuthService authService, UserService userService)
    {
        _authService = authService;
        _userService = userService;
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginRequest request)
    {
        try
        {
            var response = await _authService.LoginAsync(request);

            return Ok(response);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new
            {
                message = ex.Message
            });
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new
            {
                message = ex.Message
            });
        }
    }

    // ============================================================
    // US-08: Dispatcher creates their own account
    // POST /api/auth/register/dispatcher
    //
    // Public on purpose: the person has no account (and so no token) yet.
    // The role is always "Dispatcher" - it is never read from the request.
    // ============================================================

    [AllowAnonymous]
    [HttpPost("register/dispatcher")]
    public async Task<IActionResult> RegisterDispatcher(
        CreateUserRequest request)
    {
        try
        {
            var user =
                await _userService.RegisterDispatcherAsync(request);

            return StatusCode(StatusCodes.Status201Created, user);
        }
        catch (ValidationException ex)
        {
            return BadRequest(new
            {
                message = "Validation failed.",
                errors = ex.Errors
            });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new
            {
                message = ex.Message
            });
        }
    }
}
