using AuthUserService.Data;
using Microsoft.AspNetCore.Mvc;
using MySqlConnector;

namespace AuthUserService.Controllers;

[ApiController]
[Route("api/health")]
public class HealthController : ControllerBase
{
    private readonly DatabaseConnection _databaseConnection;

    public HealthController(DatabaseConnection databaseConnection)
    {
        _databaseConnection = databaseConnection;
    }

    [HttpGet("database")]
    public async Task<IActionResult> CheckDatabase()
    {
        try
        {
            await using var connection = _databaseConnection.CreateConnection();

            await connection.OpenAsync();

            await using var command = new MySqlCommand(
                "SELECT 1",
                connection);

            var result = await command.ExecuteScalarAsync();

            return Ok(new
            {
                database = "fleet_identity_db",
                connected = true,
                result
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new
            {
                database = "fleet_identity_db",
                connected = false,
                error = ex.Message
            });
        }
    }
}