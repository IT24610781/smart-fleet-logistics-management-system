using MySqlConnector;

namespace TrackingTelemetryService.Data;

// Thin wrapper around the connection string, same pattern as the other
// services - registered as a singleton, each repository call opens its own
// short-lived MySqlConnection from here.
public class DatabaseConnection(IConfiguration configuration)
{
    private readonly string _connectionString =
        configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("ConnectionStrings:DefaultConnection is not configured.");

    public MySqlConnection CreateConnection() => new(_connectionString);
}