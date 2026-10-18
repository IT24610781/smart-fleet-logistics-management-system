using Microsoft.Extensions.Configuration;
using MySqlConnector;
using TripDispatchService.Services;
using TripDispatchService.Data;

namespace TripDispatchService.Tests;

// Uses an isolated, automatically cleaned database on the configured test MySQL.
// Never points the guard at the application's existing database.
[Trait("Category", "Integration")]
public sealed class DriverAssignmentGuardDatabaseTests
{
    [GuardDatabaseFact]
    public async Task PreparedDeactivationBlocksFutureAssignments()
    {
        await using var fixture = await Fixture.CreateAsync();
        var id = Guid.NewGuid();
        await fixture.Guard.BlockForDeactivationAsync(id, default);
        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Guard.BeginAssignmentAsync(id, default));
        // Retrying after an interrupted Auth update is safe.
        await fixture.Guard.BlockForDeactivationAsync(id, default);
    }

    [GuardDatabaseFact]
    public async Task AssignedAndActiveTripsBlockDeactivationAndNewAssignment()
    {
        await using var fixture = await Fixture.CreateAsync();
        foreach (var status in new[] { "Assigned", "In-Progress" })
        {
            var id = Guid.NewGuid();
            await fixture.InsertAsync(id, status);
            await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Guard.BlockForDeactivationAsync(id, default));
            // Busy-driver guard: a Driver already carrying this trip cannot be
            // handed a second one until it's resolved (Completed/Cancelled/
            // rejected back to Requested). This reverses the prior behavior,
            // which allowed assigning an already-busy Driver a second trip.
            await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Guard.BeginAssignmentAsync(id, default));
        }
    }

    [GuardDatabaseFact]
    public async Task HistoricalAndOtherDriversTripsDoNotBlock()
    {
        await using var fixture = await Fixture.CreateAsync();
        var id = Guid.NewGuid();
        await fixture.InsertAsync(id, "Completed");
        await fixture.InsertAsync(id, "Cancelled");
        await fixture.InsertAsync(Guid.NewGuid(), "Assigned");
        await fixture.Guard.BlockForDeactivationAsync(id, default);
        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Guard.BeginAssignmentAsync(id, default));
    }

    [GuardDatabaseFact]
    public async Task ConcurrentAssignmentCompletesBeforeDeactivationCanCheck()
    {
        await using var fixture = await Fixture.CreateAsync();
        var id = Guid.NewGuid();
        var assignment = await fixture.Guard.BeginAssignmentAsync(id, default);
        var deactivation = fixture.Guard.BlockForDeactivationAsync(id, default);
        await Task.Delay(100);
        Assert.False(deactivation.IsCompleted);
        // The existing repository writes on another connection while the lease is held.
        await fixture.InsertAsync(id, "Assigned");
        await assignment.DisposeAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(() => deactivation);
    }

    private sealed class Fixture(string rootConnection, string databaseName, string connectionString) : IAsyncDisposable
    {
        public DriverAssignmentGuard Guard { get; } = new(new DatabaseConnection(
            new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            { ["ConnectionStrings:DefaultConnection"] = connectionString }).Build()));

        public static async Task<Fixture> CreateAsync()
        {
            var builder = new MySqlConnectionStringBuilder(Environment.GetEnvironmentVariable("TEST_TRIP_GUARD_CONNECTION")
                ?? throw new InvalidOperationException("Test MySQL connection is required."))
            { Database = "" };
            var root = builder.ConnectionString;
            var name = "qa_driver_guard_" + Guid.NewGuid().ToString("N");
            await using var connection = new MySqlConnection(root);
            await connection.OpenAsync();
            await using (var create = new MySqlCommand($"CREATE DATABASE `{name}`;", connection))
                await create.ExecuteNonQueryAsync();
            builder.Database = name;
            await using var test = new MySqlConnection(builder.ConnectionString);
            await test.OpenAsync();
            await using var schema = new MySqlCommand(
                "CREATE TABLE trips (assigned_driver_id CHAR(36), status VARCHAR(50));" +
                "CREATE TABLE driver_account_guards (driver_id CHAR(36) PRIMARY KEY, assignment_blocked BOOLEAN, updated_at DATETIME DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP);", test);
            await schema.ExecuteNonQueryAsync();
            return new Fixture(root, name, builder.ConnectionString);
        }

        public async Task InsertAsync(Guid id, string status)
        {
            await using var connection = new MySqlConnection(connectionString);
            await connection.OpenAsync();
            await using var command = new MySqlCommand("INSERT INTO trips VALUES (@id, @status);", connection);
            command.Parameters.AddWithValue("@id", id.ToString());
            command.Parameters.AddWithValue("@status", status);
            await command.ExecuteNonQueryAsync();
        }

        public async ValueTask DisposeAsync()
        {
            // Only the GUID-named database this fixture created may be cleaned up.
            if (!databaseName.StartsWith("qa_driver_guard_") || databaseName.Length != 48)
                throw new InvalidOperationException("Unexpected test database name.");
            await using var connection = new MySqlConnection(rootConnection);
            await connection.OpenAsync();
            await using var command = new MySqlCommand($"DROP DATABASE `{databaseName}`;", connection);
            await command.ExecuteNonQueryAsync();
        }
    }
}

public sealed class GuardDatabaseFactAttribute : FactAttribute
{
    public GuardDatabaseFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("TEST_TRIP_GUARD_CONNECTION")))
            Skip = "Set TEST_TRIP_GUARD_CONNECTION to a test MySQL server with create/drop-database permissions.";
    }
}