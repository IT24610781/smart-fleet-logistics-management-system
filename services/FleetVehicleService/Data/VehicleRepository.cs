using FleetVehicleService.Models;
using MySqlConnector;

namespace FleetVehicleService.Data;

public class VehicleRepository
{
    private readonly DatabaseConnection _databaseConnection;

    public VehicleRepository(DatabaseConnection databaseConnection)
    {
        _databaseConnection = databaseConnection;
    }

    // ============================================================
    // US-04: Create vehicle
    // ============================================================

    public async Task CreateVehicleAsync(Vehicle vehicle)
    {
        const string sql = """
            INSERT INTO vehicles
            (
                id,
                registration_no,
                type,
                capacity,
                status
            )
            VALUES
            (
                @id,
                @registration_no,
                @type,
                @capacity,
                @status
            );
            """;

        await using var connection =
            _databaseConnection.CreateConnection();

        await connection.OpenAsync();

        await using var command =
            new MySqlCommand(sql, connection);

        command.Parameters.AddWithValue("@id", vehicle.Id.ToString());
        command.Parameters.AddWithValue("@registration_no", vehicle.RegistrationNo);
        command.Parameters.AddWithValue("@type", vehicle.Type);
        command.Parameters.AddWithValue("@capacity", vehicle.Capacity);
        command.Parameters.AddWithValue("@status", vehicle.Status);

        await command.ExecuteNonQueryAsync();
    }


    // ============================================================
    // US-04: Find vehicle by registration number (duplicate check)
    // ============================================================

    public async Task<Vehicle?> GetByRegistrationNoAsync(string registrationNo)
    {
        const string sql = """
            SELECT
                id,
                registration_no,
                type,
                capacity,
                status,
                created_at,
                updated_at
            FROM vehicles
            WHERE registration_no = @registration_no
            LIMIT 1;
            """;

        await using var connection =
            _databaseConnection.CreateConnection();

        await connection.OpenAsync();

        await using var command =
            new MySqlCommand(sql, connection);

        command.Parameters.AddWithValue("@registration_no", registrationNo);

        await using var reader =
            await command.ExecuteReaderAsync();

        if (await reader.ReadAsync())
        {
            return MapVehicle(reader);
        }

        return null;
    }


    // ============================================================
    // Map database row -> Vehicle model
    // ============================================================

    private static Vehicle MapVehicle(MySqlDataReader reader)
    {
        return new Vehicle
        {
            Id = reader.GetGuid("id"),

            RegistrationNo = reader.GetString("registration_no"),

            Type = reader.GetString("type"),

            Capacity = reader.GetInt32("capacity"),

            Status = reader.GetString("status"),

            CreatedAt = reader.GetDateTime("created_at"),

            UpdatedAt = reader.GetDateTime("updated_at")
        };
    }
}
