using MySqlConnector;
using TripDispatchService.Data;

namespace TripDispatchService.Services;

// MySQL advisory locks coordinate all Trip service instances using the same DB.
// The persistent block closes the gap between Trip's check and Auth's update.
public class DriverAssignmentGuard(DatabaseConnection database)
{
    public async Task<IAsyncDisposable> BeginAssignmentAsync(Guid driverId, CancellationToken token)
    {
        var lease = await AcquireAsync(driverId, token);
        try
        {
            await using var command = new MySqlCommand(
                "SELECT COUNT(*) FROM driver_account_guards WHERE driver_id = @id AND assignment_blocked = TRUE;", lease.Connection);
            command.Parameters.AddWithValue("@id", driverId.ToString());
            if (Convert.ToInt64(await command.ExecuteScalarAsync(token)) != 0)
                throw new InvalidOperationException("This Driver is inactive or has a pending deactivation and cannot receive new assignments.");

            await using var activeTripCheck = new MySqlCommand(
                "SELECT COUNT(*) FROM trips WHERE assigned_driver_id = @id AND status IN ('Assigned', 'In-Progress');", lease.Connection);
            activeTripCheck.Parameters.AddWithValue("@id", driverId.ToString());
            if (Convert.ToInt64(await activeTripCheck.ExecuteScalarAsync(token)) != 0)
                throw new InvalidOperationException("This Driver already has an assigned or in-progress trip and cannot be assigned another until it is completed.");

            return lease;
        }
        catch { await lease.DisposeAsync(); throw; }
    }

    public async Task BlockForDeactivationAsync(Guid driverId, CancellationToken token)
    {
        await using var lease = await AcquireAsync(driverId, token);
        await using var check = new MySqlCommand(
            "SELECT COUNT(*) FROM trips WHERE assigned_driver_id = @id AND status IN ('Assigned', 'In-Progress');", lease.Connection);
        check.Parameters.AddWithValue("@id", driverId.ToString());
        if (Convert.ToInt64(await check.ExecuteScalarAsync(token)) != 0)
            throw new InvalidOperationException("This Driver has an assigned or active trip. Complete or resolve the assignment before deactivating the account.");
        await using var block = new MySqlCommand(
            "INSERT INTO driver_account_guards (driver_id, assignment_blocked) VALUES (@id, TRUE) ON DUPLICATE KEY UPDATE assignment_blocked = TRUE;", lease.Connection);
        block.Parameters.AddWithValue("@id", driverId.ToString());
        await block.ExecuteNonQueryAsync(token);
    }

    private async Task<Lease> AcquireAsync(Guid driverId, CancellationToken token)
    {
        var connection = database.CreateConnection();
        try
        {
            await connection.OpenAsync(token);
            // Include DB name so independent test/staging databases do not share locks.
            var key = $"driver:{connection.Database}:{driverId}";
            if (key.Length > 64) key = "driver:" + driverId;
            await using var command = new MySqlCommand("SELECT GET_LOCK(@key, 5);", connection);
            command.Parameters.AddWithValue("@key", key);
            if (Convert.ToInt32(await command.ExecuteScalarAsync(token)) != 1)
                throw new DriverCoordinationUnavailableException("The Driver is being updated. Try again shortly.");
            return new Lease(connection, key);
        }
        catch { await connection.DisposeAsync(); throw; }
    }

    private sealed class Lease(MySqlConnection connection, string key) : IAsyncDisposable
    {
        public MySqlConnection Connection => connection;
        public async ValueTask DisposeAsync()
        {
            try
            {
                if (connection.State == System.Data.ConnectionState.Open)
                {
                    await using var command = new MySqlCommand("SELECT RELEASE_LOCK(@key);", connection);
                    command.Parameters.AddWithValue("@key", key);
                    await command.ExecuteScalarAsync();
                }
            }
            finally { await connection.DisposeAsync(); }
        }
    }
}

public class DriverCoordinationUnavailableException(string message) : Exception(message);