using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TripDispatchService.DTOs;
using TripDispatchService.Services;

namespace TripDispatchService.Controllers;

[ApiController]
[Route("api/trips")]
public class TripsController : ControllerBase
{
    private readonly TripService _tripService;

    private readonly DriverEligibilityClient _drivers;
    private readonly DriverAssignmentGuard _driverGuard;

    public TripsController(TripService tripService, DriverEligibilityClient drivers, DriverAssignmentGuard driverGuard)
    {
        _tripService = tripService;
        _drivers = drivers;
        _driverGuard = driverGuard;
    }

    //Trip Details page fetch one trip's full information

    [Authorize(Roles = "Dispatcher,Admin")]
    [HttpGet("{id}")]
    public async Task<IActionResult> GetTrip(Guid id)
    {
        try
        {
            var trip = await _tripService.GetTripByIdAsync(id);
            return Ok(trip);
        }
        catch (NotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    //Create-Trip page preview ranked vehicles before the trip exists

    [Authorize(Roles = "Dispatcher")]
    [HttpGet("preview-suggestions")]
    public async Task<IActionResult> PreviewSuggestions([FromQuery] string? capacity)
    {
        try
        {
            var authorizationHeader = Request.Headers.Authorization.ToString();
            var suggestions = await _tripService.GetSuggestionsForCapacityAsync(capacity, authorizationHeader);
            return Ok(suggestions);
        }
        catch (ValidationException ex)
        {
            return BadRequest(new
            {
                message = "Validation failed.",
                errors = ex.Errors
            });
        }
    }

    //US-10 Ranked vehicle suggestions for a trip

    [Authorize(Roles = "Dispatcher")]
    [HttpGet("{id}/suggestions")]
    public async Task<IActionResult> GetSuggestions(Guid id)
    {
        try
        {
            var authorizationHeader = Request.Headers.Authorization.ToString();
            var suggestions = await _tripService.GetSuggestionsAsync(id, authorizationHeader);
            return Ok(suggestions);
        }
        catch (NotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    //US-11/US-12 Dispatcher assigns a vehicle AND driver to a trip

    [Authorize(Roles = "Dispatcher")]
    [HttpPost("{id}/assign")]
    public async Task<IActionResult> AssignVehicle(Guid id, AssignVehicleRequest request)
    {
        try
        {
            var authorizationHeader = Request.Headers.Authorization.ToString();
            await using var lease = request.DriverId.HasValue
                ? await _driverGuard.BeginAssignmentAsync(request.DriverId.Value, HttpContext.RequestAborted) : null;
            if (request.DriverId.HasValue)
                await _drivers.EnsureActiveAsync(request.DriverId.Value, authorizationHeader, HttpContext.RequestAborted);
            var trip = await _tripService.AssignVehicleAsync(id, request.VehicleId, request.DriverId, authorizationHeader);
            return Ok(trip);
        }
        catch (Exception ex) when (ex is MySqlConnector.MySqlException or DriverCoordinationUnavailableException)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { message = "Driver assignment could not be verified. Try again shortly." });
        }
        catch (DriverEligibilityUnavailableException ex)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { message = ex.Message });
        }
        catch (ValidationException ex)
        {
            return BadRequest(new
            {
                message = "Validation failed.",
                errors = ex.Errors
            });
        }
        catch (NotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }

    // Auth calls this with the Admin token before making the account inactive.
    [Authorize(Roles = "Admin")]
    [HttpPost("drivers/{driverId:guid}/deactivation-check")]
    public async Task<IActionResult> PrepareDriverDeactivation(Guid driverId, CancellationToken token)
    {
        try
        {
            await _driverGuard.BlockForDeactivationAsync(driverId, token);
            return NoContent();
        }
        catch (InvalidOperationException ex) { return Conflict(new { message = ex.Message }); }
        catch (Exception ex) when (ex is MySqlConnector.MySqlException or DriverCoordinationUnavailableException)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { message = "Driver trip eligibility could not be verified. Try again shortly." });
        }
    }

    //US-12 Driver views their own trips

    [Authorize(Roles = "Driver")]
    [HttpGet("mine")]
    public async Task<IActionResult> GetMyTrips()
    {
        var driverId = GetCurrentUserId();
        var trips = await _tripService.GetMyTripsAsync(driverId);
        return Ok(trips);
    }

    //US-12 Driver accepts or rejects an assigned trip

    [Authorize(Roles = "Driver")]
    [HttpPost("{id}/respond")]
    public async Task<IActionResult> Respond(Guid id, RespondToTripRequest request)
    {
        try
        {
            var driverId = GetCurrentUserId();
            var authorizationHeader = Request.Headers.Authorization.ToString();
            var trip = await _tripService.RespondAsync(id, driverId, request.Decision, authorizationHeader);
            return Ok(trip);
        }
        catch (ValidationException ex)
        {
            return BadRequest(new
            {
                message = "Validation failed.",
                errors = ex.Errors
            });
        }
        catch (NotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }

    //Same claim-reading pattern as AuthUserService.Controllers.UsersController.
    private Guid GetCurrentUserId()
    {
        var userIdClaim =
            User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue("sub");

        if (!Guid.TryParse(userIdClaim, out var userId))
        {
            throw new UnauthorizedAccessException("Invalid user identity.");
        }

        return userId;
    }

    //US-13 List trips, optionally filtered by status

    [Authorize(Roles = "Dispatcher,Admin")]
    [HttpGet]
    public async Task<IActionResult> GetTrips([FromQuery] string? status)
    {
        try
        {
            var trips = await _tripService.GetTripsAsync(status);
            return Ok(trips);
        }
        catch (ValidationException ex)
        {
            return BadRequest(new
            {
                message = "Validation failed.",
                errors = ex.Errors
            });
        }
    }

    //US-09 Dispatcher creates a trip request

    [Authorize(Roles = "Dispatcher")]
    [HttpPost]
    public async Task<IActionResult> CreateTrip(
        CreateTripRequest request)
    {
        try
        {
        var dispatcherId = GetCurrentUserId();
            var trip =
                await _tripService.CreateTripAsync(request, dispatcherId);

            return CreatedAtAction(
                nameof(CreateTrip),
                new { id = trip.Id },
                trip);
        }
        catch (ValidationException ex)
        {
            return BadRequest(new
            {
                message = "Validation failed.",
                errors = ex.Errors
            });
        }
    }
}