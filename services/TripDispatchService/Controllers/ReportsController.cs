using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TripDispatchService.Services;

namespace TripDispatchService.Controllers;

[ApiController]
[Route("api/reports")]
public class ReportsController(TripCompletionService tripCompletionService) : ControllerBase
{
    //SCRUM-143 (US-18) Trip completion summary for a reporting period

    [Authorize(Roles = "Dispatcher,Admin")]
    [HttpGet("trip-completion")]
    public async Task<IActionResult> GetTripCompletion(
        [FromQuery] DateTime? start, [FromQuery] DateTime? end, CancellationToken cancellationToken)
    {
        try
        {
            var report = await tripCompletionService.GetTripCompletionAsync(start, end, cancellationToken);
            return Ok(report);
        }
        catch (ValidationException ex)
        {
            return BadRequest(new { message = "Validation failed.", errors = ex.Errors });
        }
    }
}