using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TrackingTelemetryService.DTOs;
using TrackingTelemetryService.Services;

namespace TrackingTelemetryService.Controllers;

[ApiController]
[Route("api/tracking")]
public class TrackingController(TrackingService trackingService) : ControllerBase
{
    //US-14- Driver sends a live location update for their own In progress Trip

    [Authorize(Roles = "Driver")]
    [HttpPost("locations")]
    public async Task<IActionResult> SubmitLocation(
        SubmitLocationRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var driverId = GetCurrentUserId();
            var authorizationHeader = Request.Headers.Authorization.ToString();

            var result = await trackingService.SubmitLocationAsync(
                driverId, request, authorizationHeader, cancellationToken);

            return CreatedAtAction(nameof(SubmitLocation), new { id = result.Id }, result);
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
        catch (TripServiceUnavailableException ex)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { message = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new { message = ex.Message });
        }
    }

    //SCRUM-134- Authorized location-history retrieval for one trip

    [Authorize(Roles = "Dispatcher,Admin,Driver")]
    [HttpGet("trips/{tripId}/locations")]
    public async Task<IActionResult> GetHistory(
        Guid tripId, [FromQuery] int? limit, CancellationToken cancellationToken)
    {
        try
        {
            var authorizationHeader = Request.Headers.Authorization.ToString();

            // A Driver may only see their own trip's history; Dispatcher/
            // Admin may view any trip.
            Guid? callerDriverId = User.IsInRole("Driver") ? GetCurrentUserId() : null;

            var history = await trackingService.GetHistoryAsync(
                tripId, callerDriverId, limit, authorizationHeader, cancellationToken);

            return Ok(history);
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
        catch (TripServiceUnavailableException ex)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { message = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            // Closes a gap from the original SubmitLocation endpoint: a
            // malformed/missing identity claim should be a clean 401, not
            // an unhandled 500.
            return Unauthorized(new { message = ex.Message });
        }
    }

    // Same claim-reading pattern as the other services.
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
}