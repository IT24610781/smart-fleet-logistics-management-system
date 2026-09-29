using AuthUserService.Models;
using AuthUserService.Services;
using MySqlConnector;

namespace AuthUserService.Data;

public record PasswordChallenge(Guid Id, Guid UserId, string Purpose, string CodeHash, string PasswordStamp, DateTime ExpiresAt);
public interface IPasswordChallengeRepository
{
    Task<bool> CreateAsync(PasswordChallenge challenge, string currentHash, CancellationToken cancellationToken);
    Task<bool> CompleteAsync(Guid id, Guid? userId, string purpose, string code, string newHash, string key, CancellationToken cancellationToken);
    Task<bool> HasChangedAsync(Guid userId, CancellationToken cancellationToken);
}

public sealed class PasswordChallengeRepository(DatabaseConnection database) : IPasswordChallengeRepository
{
    public async Task<bool> HasChangedAsync(Guid userId, CancellationToken cancellationToken)
    {
        await using var connection = database.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = new MySqlCommand("SELECT COUNT(*) FROM password_security WHERE user_id=@user;", connection);
        command.Parameters.AddWithValue("@user", userId.ToString());
        return Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken)) != 0;
    }

    public async Task<bool> CreateAsync(PasswordChallenge challenge, string currentHash, CancellationToken cancellationToken)
    {
        await using var connection = database.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        // Serialize requests for an account, across service instances and both purposes.
        await using var locked = new MySqlCommand("SELECT password_hash FROM users WHERE id=@user AND is_active=TRUE FOR UPDATE;", connection, transaction);
        locked.Parameters.AddWithValue("@user", challenge.UserId.ToString());
        if ((string?)await locked.ExecuteScalarAsync(cancellationToken) != currentHash) return false;
        await using var limits = new MySqlCommand("SELECT COUNT(*), MAX(created_at) FROM password_challenges WHERE user_id=@user AND created_at>UTC_TIMESTAMP(6)-INTERVAL 1 HOUR;", connection, transaction);
        limits.Parameters.AddWithValue("@user", challenge.UserId.ToString());
        await using (var reader = await limits.ExecuteReaderAsync(cancellationToken))
        {
            await reader.ReadAsync(cancellationToken);
            if (reader.GetInt32(0) >= 5 || !reader.IsDBNull(1) && reader.GetDateTime(1) > DateTime.UtcNow.AddSeconds(-60)) return false;
        }
        await using var command = new MySqlCommand("""
            UPDATE password_challenges SET consumed=TRUE WHERE user_id=@user;
            INSERT INTO password_challenges(id,user_id,purpose,code_hash,password_stamp,created_at,expires_at)
            VALUES(@id,@user,@purpose,@hash,@stamp,UTC_TIMESTAMP(6),@expires);
            """, connection, transaction);
        command.Parameters.AddWithValue("@id", challenge.Id.ToString());
        command.Parameters.AddWithValue("@user", challenge.UserId.ToString());
        command.Parameters.AddWithValue("@purpose", challenge.Purpose);
        command.Parameters.AddWithValue("@hash", challenge.CodeHash);
        command.Parameters.AddWithValue("@stamp", challenge.PasswordStamp);
        command.Parameters.AddWithValue("@expires", challenge.ExpiresAt);
        await command.ExecuteNonQueryAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return true;
    }

    public async Task<bool> CompleteAsync(Guid id, Guid? userId, string purpose, string code, string newHash, string key, CancellationToken cancellationToken)
    {
        await using var connection = database.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        // Read identity first, then always lock user before challenge (same order as CreateAsync).
        await using var identity = new MySqlCommand("SELECT user_id FROM password_challenges WHERE id=@id;", connection);
        identity.Parameters.AddWithValue("@id", id.ToString());
        var storedUser = (await identity.ExecuteScalarAsync(cancellationToken))?.ToString();
        if (!Guid.TryParse(storedUser, out var accountId) || userId.HasValue && userId.Value != accountId) return false;
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await using var account = new MySqlCommand("SELECT password_hash FROM users WHERE id=@user AND is_active=TRUE FOR UPDATE;", connection, transaction);
        account.Parameters.AddWithValue("@user", storedUser);
        var oldHash = (string?)await account.ExecuteScalarAsync(cancellationToken);
        if (oldHash is null) return false;
        await using var read = new MySqlCommand("SELECT purpose,code_hash,password_stamp,expires_at,attempts,consumed FROM password_challenges WHERE id=@id FOR UPDATE;", connection, transaction);
        read.Parameters.AddWithValue("@id", id.ToString());
        bool valid;
        await using (var reader = await read.ExecuteReaderAsync(cancellationToken))
        {
            if (!await reader.ReadAsync(cancellationToken)) return false;
            valid = reader.GetString(0) == purpose && reader.GetDateTime(3) > DateTime.UtcNow && reader.GetInt32(4) < 5 && !reader.GetBoolean(5)
                && PasswordSecurity.Matches(reader.GetString(1), PasswordSecurity.CodeHash(id, code, key))
                && PasswordSecurity.Matches(reader.GetString(2), PasswordSecurity.Stamp(oldHash, key));
        }
        await using var attempt = new MySqlCommand("UPDATE password_challenges SET attempts=attempts+1 WHERE id=@id;", connection, transaction);
        attempt.Parameters.AddWithValue("@id", id.ToString());
        await attempt.ExecuteNonQueryAsync(cancellationToken);
        if (!valid) { await transaction.CommitAsync(cancellationToken); return false; }
        await using var update = new MySqlCommand("""
            UPDATE users SET password_hash=@hash,updated_at=UTC_TIMESTAMP() WHERE id=@user;
            UPDATE password_challenges SET consumed=TRUE WHERE user_id=@user;
            INSERT INTO password_security(user_id,changed_at) VALUES(@user,UTC_TIMESTAMP(6))
                ON DUPLICATE KEY UPDATE changed_at=UTC_TIMESTAMP(6);
            """, connection, transaction);
        update.Parameters.AddWithValue("@user", storedUser);
        update.Parameters.AddWithValue("@hash", newHash);
        await update.ExecuteNonQueryAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return true;
    }
}
