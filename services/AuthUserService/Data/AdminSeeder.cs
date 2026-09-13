using AuthUserService.Models;
using MySqlConnector;

namespace AuthUserService.Data;

public class AdminSeeder
{
    private readonly DatabaseConnection _databaseConnection;

    public AdminSeeder(DatabaseConnection databaseConnection)
    {
        _databaseConnection = databaseConnection;
    }

    public async Task SeedAsync()
    {
        const string checkSql = """
            SELECT COUNT(*)
            FROM users
            WHERE role = 'Admin';
            """;

        await using var connection =
            _databaseConnection.CreateConnection();

        await connection.OpenAsync();

        await using var checkCommand =
            new MySqlCommand(checkSql, connection);

        var adminCount =
            Convert.ToInt32(await checkCommand.ExecuteScalarAsync());

        if (adminCount > 0)
        {
            Console.WriteLine("Initial Admin already exists. Skipping seed.");
            return;
        }

        var admin = new User
        {
            Id = Guid.CreateVersion7(),

            Username = "admin",

            Email = "admin@smartfleet.com",

            // Change this password before using the application.
            PasswordHash =
                BCrypt.Net.BCrypt.HashPassword("Admin123!"),

            FirstName = "System",

            LastName = "Administrator",

            ContactNumber = "0000000000",

            Role = "Admin",

            IsActive = true,

            CreatedAt = DateTime.UtcNow,

            UpdatedAt = DateTime.UtcNow
        };

        const string insertSql = """
            INSERT INTO users
            (
                id,
                username,
                email,
                password_hash,
                first_name,
                last_name,
                contact_number,
                role,
                is_active
            )
            VALUES
            (
                @id,
                @username,
                @email,
                @password_hash,
                @first_name,
                @last_name,
                @contact_number,
                @role,
                @is_active
            );
            """;

        await using var insertCommand =
            new MySqlCommand(insertSql, connection);

        insertCommand.Parameters.AddWithValue("@id", admin.Id.ToString());
        insertCommand.Parameters.AddWithValue("@username", admin.Username);
        insertCommand.Parameters.AddWithValue("@email", admin.Email);
        insertCommand.Parameters.AddWithValue("@password_hash", admin.PasswordHash);
        insertCommand.Parameters.AddWithValue("@first_name", admin.FirstName);
        insertCommand.Parameters.AddWithValue("@last_name", admin.LastName);
        insertCommand.Parameters.AddWithValue("@contact_number", admin.ContactNumber);
        insertCommand.Parameters.AddWithValue("@role", admin.Role);
        insertCommand.Parameters.AddWithValue("@is_active", admin.IsActive);

                try
        {
            await insertCommand.ExecuteNonQueryAsync();
            Console.WriteLine("Initial Admin account created successfully.");
        }
        catch (MySqlException ex) when (ex.Number == 1062)
        {
           
            Console.WriteLine(
                "Initial Admin was created concurrently by another " +
                "process. Skipping.");
        }
    }
}