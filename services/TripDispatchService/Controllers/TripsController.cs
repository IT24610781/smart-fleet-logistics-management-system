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

    public TripsController(TripService tripService)
    {
        _tripService = tripService;
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
            var trip = await _tripService.AssignVehicleAsync(id, request.VehicleId, request.DriverId, authorizationHeader);
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
            var trip =
                await _tripService.CreateTripAsync(request);

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