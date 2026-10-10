using TripDispatchService.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MySqlConnector;

namespace TripDispatchService.Controllers;

[ApiController]
[AllowAnonymous]
[Route("health")]
public sealed class HealthController(DatabaseConnection database) : ControllerBase
{
    [HttpGet("live")]
    public IActionResult Live() => Ok(new { status = "alive" });

    [HttpGet("ready")]
    public async Task<IActionResult> Ready(CancellationToken cancellationToken)
    {
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(3));
            await using var connection = database.CreateConnection();
            await connection.OpenAsync(timeout.Token);
            await using var command = new MySqlCommand("SELECT 1", connection);
            await command.ExecuteScalarAsync(timeout.Token);
            return Ok(new { status = "ready" });
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { status = "not ready" });
        }
    }
}
