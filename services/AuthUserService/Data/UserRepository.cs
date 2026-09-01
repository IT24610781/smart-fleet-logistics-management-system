using AuthUserService.Models;
using MySqlConnector;

namespace AuthUserService.Data;

public class UserRepository
{
    private readonly DatabaseConnection _databaseConnection;

    public UserRepository(DatabaseConnection databaseConnection)
    {
        _databaseConnection = databaseConnection;
    }

    public async Task CreateUserAsync(User user)
    {
        const string sql = """
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

        await using var connection =
            _databaseConnection.CreateConnection();

        await connection.OpenAsync();

        await using var command =
            new MySqlCommand(sql, connection);

        command.Parameters.AddWithValue("@id", user.Id.ToString());
        command.Parameters.AddWithValue("@username", user.Username);
        command.Parameters.AddWithValue("@email", user.Email);
        command.Parameters.AddWithValue("@password_hash", user.PasswordHash);
        command.Parameters.AddWithValue("@first_name", user.FirstName);
        command.Parameters.AddWithValue("@last_name", user.LastName);
        command.Parameters.AddWithValue("@contact_number", user.ContactNumber);
        command.Parameters.AddWithValue("@role", user.Role);
        command.Parameters.AddWithValue("@is_active", user.IsActive);

        await command.ExecuteNonQueryAsync();
    }

    public async Task<User?> GetByUsernameAsync(string username)
    {
        const string sql = """
            SELECT id, username, email, password_hash,
                   first_name, last_name, contact_number,
                   role, is_active, created_at, updated_at
            FROM users
            WHERE username = @username
            LIMIT 1;
            """;

        await using var connection =
            _databaseConnection.CreateConnection();

        await connection.OpenAsync();

        await using var command =
            new MySqlCommand(sql, connection);

        command.Parameters.AddWithValue("@username", username);

        await using var reader =
            await command.ExecuteReaderAsync();

        if (await reader.ReadAsync())
        {
            return MapUser(reader);
        }

        return null;
    }

    public async Task<User?> GetByEmailAsync(string email)
    {
        const string sql = """
            SELECT id, username, email, password_hash,
                   first_name, last_name, contact_number,
                   role, is_active, created_at, updated_at
            FROM users
            WHERE email = @email
            LIMIT 1;
            """;

        await using var connection =
            _databaseConnection.CreateConnection();

        await connection.OpenAsync();

        await using var command =
            new MySqlCommand(sql, connection);

        command.Parameters.AddWithValue("@email", email);

        await using var reader =
            await command.ExecuteReaderAsync();

        if (await reader.ReadAsync())
        {
            return MapUser(reader);
        }

        return null;
    }

    private static User MapUser(MySqlDataReader reader)
    {
        return new User
        {
            Id = Guid.Parse(reader.GetString("id")),
            Username = reader.GetString("username"),
            Email = reader.GetString("email"),
            PasswordHash = reader.GetString("password_hash"),
            FirstName = reader.GetString("first_name"),
            LastName = reader.GetString("last_name"),
            ContactNumber = reader.GetString("contact_number"),
            Role = reader.GetString("role"),
            IsActive = reader.GetBoolean("is_active"),
            CreatedAt = reader.GetDateTime("created_at"),
            UpdatedAt = reader.GetDateTime("updated_at")
        };
    }
}