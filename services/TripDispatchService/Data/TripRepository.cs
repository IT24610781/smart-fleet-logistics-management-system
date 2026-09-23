using MySqlConnector;
using TripDispatchService.Models;

namespace TripDispatchService.Data;

public class TripRepository : ITripRepository
{
    private readonly DatabaseConnection _databaseConnection;

    public TripRepository(DatabaseConnection databaseConnection)
    {
        _databaseConnection = databaseConnection;
    }

    // US-09: Create a trip request

    public async Task CreateTripAsync(Trip trip)
    {
        const string sql = """
            INSERT INTO trips
            (
                id,
                pickup_location,
                dropoff_location,
                required_capacity,
                status,
                assigned_vehicle_id
            )
            VALUES
            (
                @id,
                @pickup_location,
                @dropoff_location,
                @required_capacity,
                @status,
                @assigned_vehicle_id
            );
            """;

        await using var connection =
            _databaseConnection.CreateConnection();

        await connection.OpenAsync();

        await using var command =
            new MySqlCommand(sql, connection);

        command.Parameters.AddWithValue("@id", trip.Id.ToString());
        command.Parameters.AddWithValue("@pickup_location", trip.PickupLocation);
        command.Parameters.AddWithValue("@dropoff_location", trip.DropoffLocation);
        command.Parameters.AddWithValue("@required_capacity", trip.RequiredCapacity);
        command.Parameters.AddWithValue("@status", trip.Status);
        command.Parameters.AddWithValue(
            "@assigned_vehicle_id",
            trip.AssignedVehicleId?.ToString() ?? (object)DBNull.Value);

                await command.ExecuteNonQueryAsync();
    }

    public async Task<List<Trip>> GetAllAsync(string? status)
    {
        var sql = """
            SELECT
                id,
                pickup_location,
                dropoff_location,
                required_capacity,
                status,
                assigned_vehicle_id,
                created_at,
                updated_at
            FROM trips
            """;

        if (!string.IsNullOrWhiteSpace(status))
        {
            sql += " WHERE status = @status";
        }

        sql += " ORDER BY created_at DESC;";

        await using var connection =
            _databaseConnection.CreateConnection();

        await connection.OpenAsync();

        await using var command =
            new MySqlCommand(sql, connection);

        if (!string.IsNullOrWhiteSpace(status))
        {
            command.Parameters.AddWithValue("@status", status);
        }

        await using var reader = await command.ExecuteReaderAsync();

        var trips = new List<Trip>();

        while (await reader.ReadAsync())
        {
            trips.Add(MapTrip(reader));
        }

        return trips;
    }

    // Map database row - Trip model

    private static Trip MapTrip(MySqlDataReader reader)
    {
        var assignedVehicleOrdinal = reader.GetOrdinal("assigned_vehicle_id");

        return new Trip
        {
            Id = reader.GetGuid("id"),
            PickupLocation = reader.GetString("pickup_location"),
            DropoffLocation = reader.GetString("dropoff_location"),
            RequiredCapacity = reader.GetString("required_capacity"),
            Status = reader.GetString("status"),
            AssignedVehicleId = reader.IsDBNull(assignedVehicleOrdinal)
                ? null
                : reader.GetGuid(assignedVehicleOrdinal),
            CreatedAt = reader.GetDateTime("created_at"),
            UpdatedAt = reader.GetDateTime("updated_at")
        };
    }
}