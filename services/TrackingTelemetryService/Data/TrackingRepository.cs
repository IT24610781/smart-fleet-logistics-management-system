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

        public async Task<List<TripLocationUpdate>> GetByTripIdAsync(Guid tripId, int limit)
    {
        const string sql = """
            SELECT
                id,
                trip_id,
                driver_id,
                vehicle_id,
                latitude,
                longitude,
                observed_at,
                received_at
            FROM trip_location_updates
            WHERE trip_id = @trip_id
            ORDER BY observed_at ASC
            LIMIT @limit;
            """;

        await using var connection = databaseConnection.CreateConnection();
        await connection.OpenAsync();

        await using var command = new MySqlCommand(sql, connection);
        command.Parameters.AddWithValue("@trip_id", tripId.ToString());
        command.Parameters.AddWithValue("@limit", limit);

        await using var reader = await command.ExecuteReaderAsync();

        var results = new List<TripLocationUpdate>();

        while (await reader.ReadAsync())
        {
            results.Add(new TripLocationUpdate
            {
                Id = reader.GetGuid("id"),
                TripId = reader.GetGuid("trip_id"),
                DriverId = reader.GetGuid("driver_id"),
                VehicleId = reader.GetGuid("vehicle_id"),
                Latitude = reader.GetDecimal("latitude"),
                Longitude = reader.GetDecimal("longitude"),
                ObservedAt = reader.GetDateTime("observed_at"),
                ReceivedAt = reader.GetDateTime("received_at"),
            });
        }

        return results;
    }
}