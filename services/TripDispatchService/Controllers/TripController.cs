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

    //US-13: List trips, optionally filtered by status

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

    // US-09: Dispatcher creates a trip request

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