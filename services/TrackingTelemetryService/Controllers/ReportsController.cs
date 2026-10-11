using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TrackingTelemetryService.Services;

namespace TrackingTelemetryService.Controllers;

[ApiController]
[Route("api/reports")]
public class ReportsController(FleetUtilizationService fleetUtilizationService) : ControllerBase
{
    // SCRUM-138/139 (US-17): fleet manager role - per the existing SRS note

    [Authorize(Roles = "Admin,Dispatcher")]
    [HttpGet("fleet-utilization")]
    public async Task<IActionResult> GetFleetUtilization(
        [FromQuery] DateTime? start, [FromQuery] DateTime? end, CancellationToken cancellationToken)
    {
        try
        {
            var authorizationHeader = Request.Headers.Authorization.ToString();

            var report = await fleetUtilizationService.GetFleetUtilizationAsync(
                start, end, authorizationHeader, cancellationToken);

            return Ok(report);
        }
        catch (ValidationException ex)
        {
            return BadRequest(new
            {
                message = "Validation failed.",
                errors = ex.Errors
            });
        }
        catch (Exception ex) when (ex is TripServiceUnavailableException or FleetServiceUnavailableException)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { message = ex.Message });
        }
    }
}