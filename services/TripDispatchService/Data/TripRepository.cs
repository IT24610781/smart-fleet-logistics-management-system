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
}