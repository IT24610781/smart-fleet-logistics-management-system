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

    //US-09 Create a trip request

    public async Task CreateTripAsync(Trip trip)
    {
        const string sql = """
            INSERT INTO trips
            (
                id,
                pickup_location,
                pickup_lat,
                pickup_lng,
                dropoff_location,
                dropoff_lat,
                dropoff_lng,
                required_capacity,
                created_by_dispatcher_id,
                status,
                assigned_vehicle_id
            )
            VALUES
            (
                @id,
                @pickup_location,
                @pickup_lat,
                @pickup_lng,
                @dropoff_location,
                @dropoff_lat,
                @dropoff_lng,
                @required_capacity,
                @created_by_dispatcher_id,
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
        command.Parameters.AddWithValue("@pickup_lat", (object?)trip.PickupLat ?? DBNull.Value);
        command.Parameters.AddWithValue("@pickup_lng", (object?)trip.PickupLng ?? DBNull.Value);
        command.Parameters.AddWithValue("@dropoff_location", trip.DropoffLocation);
        command.Parameters.AddWithValue("@dropoff_lat", (object?)trip.DropoffLat ?? DBNull.Value);
        command.Parameters.AddWithValue("@dropoff_lng", (object?)trip.DropoffLng ?? DBNull.Value);
        command.Parameters.AddWithValue("@required_capacity", trip.RequiredCapacity);
        command.Parameters.AddWithValue("@created_by_dispatcher_id",trip.CreatedByDispatcherId?.ToString() ?? (object)DBNull.Value);
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
                pickup_lat,
                pickup_lng,
                dropoff_location,
                dropoff_lat,
                dropoff_lng,
                required_capacity,
                created_by_dispatcher_id,
                status,
                assigned_vehicle_id,
                assigned_driver_id,
                rejected_by_driver_id,
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

    //US-10 Look up a single trip by id

    public async Task<Trip?> GetByIdAsync(Guid id)
    {
        const string sql = """
            SELECT
                id,
                pickup_location,
                pickup_lat,
                pickup_lng,
                dropoff_location,
                dropoff_lat,
                dropoff_lng,
                required_capacity,
                created_by_dispatcher_id,
                status,
                assigned_vehicle_id,
                assigned_driver_id,
                rejected_by_driver_id,
                created_at,
                updated_at
            FROM trips
            WHERE id = @id;
            """;

        await using var connection =
            _databaseConnection.CreateConnection();

        await connection.OpenAsync();

        await using var command =
            new MySqlCommand(sql, connection);

        command.Parameters.AddWithValue("@id", id.ToString());

        await using var reader = await command.ExecuteReaderAsync();

        if (!await reader.ReadAsync())
        {
            return null;
        }

        return MapTrip(reader);
    }

    //US-11 Update a trip's status and assigned vehicle

    public async Task UpdateAsync(Trip trip)
    {
        // updated_at is not set here: the column is
        // "ON UPDATE CURRENT_TIMESTAMP", so MySQL sets it automatically.
        const string sql = """
            UPDATE trips
            SET
                status = @status,
                assigned_vehicle_id = @assigned_vehicle_id,
                assigned_driver_id = @assigned_driver_id,
                rejected_by_driver_id = @rejected_by_driver_id
            WHERE id = @id;
            """;

        await using var connection =
            _databaseConnection.CreateConnection();

        await connection.OpenAsync();

        await using var command =
            new MySqlCommand(sql, connection);

        command.Parameters.AddWithValue("@id", trip.Id.ToString());
        command.Parameters.AddWithValue("@status", trip.Status);
        command.Parameters.AddWithValue(
            "@assigned_vehicle_id",
            trip.AssignedVehicleId?.ToString() ?? (object)DBNull.Value);
        command.Parameters.AddWithValue(
            "@assigned_driver_id",
            trip.AssignedDriverId?.ToString() ?? (object)DBNull.Value);
        command.Parameters.AddWithValue(
            "@rejected_by_driver_id",
            trip.RejectedByDriverId?.ToString() ?? (object)DBNull.Value);

        await command.ExecuteNonQueryAsync();
    }

    //US-12 A driver's own trips, newest first

    public async Task<List<Trip>> GetByDriverIdAsync(Guid driverId)
    {
        const string sql = """
            SELECT
                id,
                pickup_location,
                pickup_lat,
                pickup_lng,
                dropoff_location,
                dropoff_lat,
                dropoff_lng,
                required_capacity,
                created_by_dispatcher_id,
                status,
                assigned_vehicle_id,
                assigned_driver_id,
                rejected_by_driver_id,
                created_at,
                updated_at
            FROM trips
            WHERE assigned_driver_id = @driver_id
            ORDER BY created_at DESC;
            """;

        await using var connection =
            _databaseConnection.CreateConnection();

        await connection.OpenAsync();

        await using var command =
            new MySqlCommand(sql, connection);

        command.Parameters.AddWithValue("@driver_id", driverId.ToString());

        await using var reader = await command.ExecuteReaderAsync();

        var trips = new List<Trip>();

        while (await reader.ReadAsync())
        {
            trips.Add(MapTrip(reader));
        }

        return trips;
    }

    // Map database row to Trip model

    private static Trip MapTrip(MySqlDataReader reader)
    {
                var assignedVehicleOrdinal = reader.GetOrdinal("assigned_vehicle_id");
        var assignedDriverOrdinal = reader.GetOrdinal("assigned_driver_id");
        var rejectedByDriverOrdinal = reader.GetOrdinal("rejected_by_driver_id");

        return new Trip
        {
            Id = reader.GetGuid("id"),
            PickupLocation = reader.GetString("pickup_location"),
            PickupLat = reader.IsDBNull(reader.GetOrdinal("pickup_lat")) ? null : reader.GetDecimal("pickup_lat"),
            PickupLng = reader.IsDBNull(reader.GetOrdinal("pickup_lng")) ? null : reader.GetDecimal("pickup_lng"),
            DropoffLocation = reader.GetString("dropoff_location"),
            DropoffLat = reader.IsDBNull(reader.GetOrdinal("dropoff_lat")) ? null : reader.GetDecimal("dropoff_lat"),
            DropoffLng = reader.IsDBNull(reader.GetOrdinal("dropoff_lng")) ? null : reader.GetDecimal("dropoff_lng"),
            RequiredCapacity = reader.GetString("required_capacity"),
            CreatedByDispatcherId = reader.IsDBNull(reader.GetOrdinal("created_by_dispatcher_id"))
                ? null
                : reader.GetGuid(reader.GetOrdinal("created_by_dispatcher_id")),
            Status = reader.GetString("status"),
            AssignedVehicleId = reader.IsDBNull(assignedVehicleOrdinal)
                ? null
                : reader.GetGuid(assignedVehicleOrdinal),
            AssignedDriverId = reader.IsDBNull(assignedDriverOrdinal)
                ? null
                : reader.GetGuid(assignedDriverOrdinal),
            RejectedByDriverId = reader.IsDBNull(rejectedByDriverOrdinal)
                ? null
                : reader.GetGuid(rejectedByDriverOrdinal),

            CreatedAt = DateTime.SpecifyKind(reader.GetDateTime("created_at"), DateTimeKind.Utc),
            UpdatedAt = DateTime.SpecifyKind(reader.GetDateTime("updated_at"), DateTimeKind.Utc)
        };
    }
}