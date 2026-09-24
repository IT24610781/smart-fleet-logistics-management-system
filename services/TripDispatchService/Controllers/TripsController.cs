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

    //US-11 Dispatcher assigns a vehicle to a trip

    [Authorize(Roles = "Dispatcher")]
    [HttpPost("{id}/assign")]
    public async Task<IActionResult> AssignVehicle(Guid id, AssignVehicleRequest request)
    {
        try
        {
            var authorizationHeader = Request.Headers.Authorization.ToString();
            var trip = await _tripService.AssignVehicleAsync(id, request.VehicleId, authorizationHeader);
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