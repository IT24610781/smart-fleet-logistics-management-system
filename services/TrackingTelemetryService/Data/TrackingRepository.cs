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
                ObservedAt = DateTime.SpecifyKind(reader.GetDateTime("observed_at"), DateTimeKind.Utc),
                ReceivedAt = DateTime.SpecifyKind(reader.GetDateTime("received_at"), DateTimeKind.Utc),
            });
        }

        return results;
    }

    
    public async Task<Dictionary<Guid, TripLocationUpdate>> GetLatestPerTripAsync(List<Guid> tripIds)
    {
        var result = new Dictionary<Guid, TripLocationUpdate>();

        if (tripIds.Count == 0)
        {
            return result;
        }

        var paramNames = tripIds.Select((_, i) => $"@trip_id{i}").ToList();

        var sql = $"""
            SELECT
                t1.id,
                t1.trip_id,
                t1.driver_id,
                t1.vehicle_id,
                t1.latitude,
                t1.longitude,
                t1.observed_at,
                t1.received_at
            FROM trip_location_updates t1
            INNER JOIN (
                SELECT trip_id, MAX(observed_at) AS max_observed
                FROM trip_location_updates
                WHERE trip_id IN ({string.Join(",", paramNames)})
                GROUP BY trip_id
            ) t2 ON t1.trip_id = t2.trip_id AND t1.observed_at = t2.max_observed;
            """;

        await using var connection = databaseConnection.CreateConnection();
        await connection.OpenAsync();

        await using var command = new MySqlCommand(sql, connection);
        for (var i = 0; i < tripIds.Count; i++)
        {
            command.Parameters.AddWithValue(paramNames[i], tripIds[i].ToString());
        }

        await using var reader = await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            var row = new TripLocationUpdate
            {
                Id = reader.GetGuid("id"),
                TripId = reader.GetGuid("trip_id"),
                DriverId = reader.GetGuid("driver_id"),
                VehicleId = reader.GetGuid("vehicle_id"),
                Latitude = reader.GetDecimal("latitude"),
                Longitude = reader.GetDecimal("longitude"),
                ObservedAt = DateTime.SpecifyKind(reader.GetDateTime("observed_at"), DateTimeKind.Utc),
                ReceivedAt = DateTime.SpecifyKind(reader.GetDateTime("received_at"), DateTimeKind.Utc),
            };

            result[row.TripId] = row;
        }

        return result;
    }
}