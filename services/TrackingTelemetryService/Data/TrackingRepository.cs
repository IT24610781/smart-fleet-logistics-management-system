using MySqlConnector;
using TrackingTelemetryService.Models;

namespace TrackingTelemetryService.Data;

public class TrackingRepository(DatabaseConnection databaseConnection) : ITrackingRepository
{
    public async Task CreateAsync(TripLocationUpdate update)
    {
        const string sql = """
            INSERT INTO trip_location_updates
            (
                id,
                trip_id,
                driver_id,
                vehicle_id,
                latitude,
                longitude,
                observed_at,
                received_at
            )
            VALUES
            (
                @id,
                @trip_id,
                @driver_id,
                @vehicle_id,
                @latitude,
                @longitude,
                @observed_at,
                @received_at
            );
            """;

        await using var connection = databaseConnection.CreateConnection();
        await connection.OpenAsync();

        await using var command = new MySqlCommand(sql, connection);
        command.Parameters.AddWithValue("@id", update.Id.ToString());
        command.Parameters.AddWithValue("@trip_id", update.TripId.ToString());
        command.Parameters.AddWithValue("@driver_id", update.DriverId.ToString());
        command.Parameters.AddWithValue("@vehicle_id", update.VehicleId.ToString());
        command.Parameters.AddWithValue("@latitude", update.Latitude);
        command.Parameters.AddWithValue("@longitude", update.Longitude);
        command.Parameters.AddWithValue("@observed_at", update.ObservedAt);
        command.Parameters.AddWithValue("@received_at", update.ReceivedAt);

        await command.ExecuteNonQueryAsync();
    }
}