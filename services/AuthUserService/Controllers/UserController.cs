using System.Security.Claims;
using AuthUserService.DTOs;
using AuthUserService.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AuthUserService.Controllers;

[ApiController]
[Route("api/users")]
public class UsersController : ControllerBase
{
    private readonly UserService _userService;

    private readonly DriverDeactivationService _deactivation;

    public UsersController(UserService userService, DriverDeactivationService deactivation)
    {
        _userService = userService;
        _deactivation = deactivation;
    }


    // US-01: Admin creates Driver account


    [Authorize(Roles = "Admin")]
    [HttpPost("drivers")]
    public async Task<IActionResult> CreateDriver(
        CreateUserRequest request)
    {
        try
        {
            var user =
                await _userService.CreateUserAsync(request);

            return Created($"/api/users/{user.Id}", user);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new
            {
                message = ex.Message
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

    // US-01: Admin lists Driver accounts
    // US-12: Dispatcher also needs this list to pick a driver at assign time.
    // GET /api/users/drivers

    [Authorize(Roles = "Admin,Dispatcher")]
    [HttpGet("drivers")]
    public async Task<IActionResult> GetDrivers()
    {
        var drivers = await _userService.GetDriversAsync();
        return Ok(drivers);
    }

    [Authorize(Roles = "Admin")]
    [HttpPost("drivers/{id:guid}/deactivate")]
    public async Task<IActionResult> DeactivateDriver(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            await _deactivation.DeactivateAsync(id, Request.Headers.Authorization.ToString(), cancellationToken);
            return NoContent();
        }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return Conflict(new { message = ex.Message }); }
        catch (DriverDependencyException ex)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { message = ex.Message });
        }
    }

    // US-04: View own profile
    // GET /api/users/me

    [Authorize]
    [HttpGet("me")]
    public async Task<IActionResult> GetMyProfile()
    {
        try
        {
            var userId = GetCurrentUserId();

            var user =
                await _userService.GetProfileAsync(userId);

            return Ok(user);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new
            {
                message = ex.Message
            });
        }
    }


    [Authorize]
    [HttpGet("{id}")]
    public async Task<IActionResult> GetUserById(Guid id)
    {
        try
        {
            var user = await _userService.GetProfileAsync(id);
            return Ok(user);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    // US-04: Edit own profile
    // PUT /api/users/me

    [Authorize]
    [HttpPut("me")]
    public async Task<IActionResult> UpdateMyProfile(
        UpdateProfileRequest request)
    {
        try
        {
            var userId = GetCurrentUserId();

            var user =
                await _userService.UpdateProfileAsync(
                    userId,
                    request);

            return Ok(user);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new
            {
                message = ex.Message
            });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new
            {
                message = ex.Message
            });
        }
    }

    // Get authenticated user's ID from JWT

    private Guid GetCurrentUserId()
    {
        var userIdClaim =
            User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue("sub");

        if (!Guid.TryParse(userIdClaim, out var userId))
        {
            throw new UnauthorizedAccessException(
                "Invalid user identity.");
        }

        return userId;
    }
}
