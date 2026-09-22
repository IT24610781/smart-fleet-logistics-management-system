using MySqlConnector;

namespace TripDispatchService.Data;

public class DatabaseConnection
{
    private readonly string _connectionString;

    public DatabaseConnection(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException(
                "Database connection string 'DefaultConnection' was not found.");
    }

    public MySqlConnection CreateConnection()
    {
        return new MySqlConnection(_connectionString);
    }
}