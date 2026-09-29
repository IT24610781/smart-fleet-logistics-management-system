using AuthUserService.Models;
using MySqlConnector;

namespace AuthUserService.Data;

public class UserRepository : IUserAccountRepository
{
    private readonly DatabaseConnection _databaseConnection;

    public UserRepository(DatabaseConnection databaseConnection)
    {
        _databaseConnection = databaseConnection;
    }

    // ============================================================
    // US-01: Create user
    // ============================================================

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


    // ============================================================
    // US-01: List driver users
    // ============================================================

    public async Task<List<User>> GetDriversAsync()
    {
        const string sql = """
            SELECT
                id,
                username,
                email,
                password_hash,
                first_name,
                last_name,
                contact_number,
                role,
                is_active,
                created_at,
                updated_at
            FROM users
            WHERE role = 'Driver'
            ORDER BY created_at DESC;
            """;

        await using var connection =
            _databaseConnection.CreateConnection();

        await connection.OpenAsync();

        await using var command =
            new MySqlCommand(sql, connection);

        await using var reader =
            await command.ExecuteReaderAsync();

        var users = new List<User>();
        while (await reader.ReadAsync())
        {
            users.Add(MapUser(reader));
        }

        return users;
    }


    // ============================================================
    // US-02: Find user by username
    // ============================================================

    public async Task<User?> GetByUsernameAsync(string username)
    {
        const string sql = """
            SELECT
                id,
                username,
                email,
                password_hash,
                first_name,
                last_name,
                contact_number,
                role,
                is_active,
                created_at,
                updated_at
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


    // ============================================================
    // US-01: Find user by email
    // ============================================================

    public async Task<User?> GetByEmailAsync(string email)
    {
        const string sql = """
            SELECT
                id,
                username,
                email,
                password_hash,
                first_name,
                last_name,
                contact_number,
                role,
                is_active,
                created_at,
                updated_at
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


    // ============================================================
    // US-04: Get currently logged-in user's profile
    // ============================================================

    public async Task<User?> GetByIdAsync(Guid id)
    {
        const string sql = """
            SELECT
                id,
                username,
                email,
                password_hash,
                first_name,
                last_name,
                contact_number,
                role,
                is_active,
                created_at,
                updated_at
            FROM users
            WHERE id = @id
            LIMIT 1;
            """;

        await using var connection =
            _databaseConnection.CreateConnection();

        await connection.OpenAsync();

        await using var command =
            new MySqlCommand(sql, connection);

        command.Parameters.AddWithValue("@id", id.ToString());

        await using var reader =
            await command.ExecuteReaderAsync();

        if (await reader.ReadAsync())
        {
            return MapUser(reader);
        }

        return null;
    }


    // ============================================================
    // US-04: Update currently logged-in user's profile
    // ============================================================

    public async Task UpdateProfileAsync(User user)
    {
        const string sql = """
            UPDATE users
            SET
                first_name = @first_name,
                last_name = @last_name,
                contact_number = @contact_number,
                updated_at = CURRENT_TIMESTAMP
            WHERE id = @id;
            """;

        await using var connection =
            _databaseConnection.CreateConnection();

        await connection.OpenAsync();

        await using var command =
            new MySqlCommand(sql, connection);

        command.Parameters.AddWithValue(
            "@id",
            user.Id.ToString());

        command.Parameters.AddWithValue(
            "@first_name",
            user.FirstName);

        command.Parameters.AddWithValue(
            "@last_name",
            user.LastName);

        command.Parameters.AddWithValue(
            "@contact_number",
            user.ContactNumber);

        await command.ExecuteNonQueryAsync();
    }


    // ============================================================
    // Map database row -> User model
    // ============================================================

    public async Task DeactivateDriverAsync(Guid id)
    {
        await using var connection = _databaseConnection.CreateConnection();
        await connection.OpenAsync();
        await using var command = new MySqlCommand(
            "UPDATE users SET is_active = FALSE, updated_at = CURRENT_TIMESTAMP WHERE id = @id AND role = 'Driver' AND is_active = TRUE;", connection);
        command.Parameters.AddWithValue("@id", id.ToString());
        await command.ExecuteNonQueryAsync();
    }

    private static User MapUser(MySqlDataReader reader)
    {
        return new User
        {
            Id = reader.GetGuid("id"),

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
