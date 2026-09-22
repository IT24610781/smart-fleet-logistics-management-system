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