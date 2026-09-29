using System.Security.Cryptography;
using AuthUserService.Data;

namespace AuthUserService.Services;

public record PasswordCodeResponse(Guid ChallengeId, string MaskedEmail, string Message, int ExpiresInSeconds = 600, int ResendAfterSeconds = 60);
public sealed class PasswordService(UserRepository users, IPasswordChallengeRepository challenges,
    IPasswordEmailSender email, IConfiguration configuration, ILogger<PasswordService> logger)
{
    private string Key => configuration["Jwt:Key"] ?? throw new InvalidOperationException("JWT key is missing.");

    public async Task<PasswordCodeResponse> RequestAsync(string address, Guid? userId, string? currentPassword, CancellationToken cancellationToken)
    {
        if (!email.IsConfigured) throw new PasswordEmailUnavailableException();
        var user = userId.HasValue ? await users.GetByIdAsync(userId.Value) : await users.GetByEmailAsync(address.Trim());
        if (userId.HasValue && (user is null || !user.IsActive || string.IsNullOrEmpty(currentPassword) || !BCrypt.Net.BCrypt.Verify(currentPassword, user.PasswordHash)))
            throw new ArgumentException("Your current password is incorrect.");
        var id = Guid.NewGuid();
        var masked = PasswordSecurity.MaskEmail(userId.HasValue ? user!.Email : address.Trim());
        var response = new PasswordCodeResponse(id, masked, "If this email belongs to an active account, a verification code will arrive shortly.");
        if (user is null || !user.IsActive) return response;
        var code = RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");
        var challenge = new PasswordChallenge(id, user.Id, userId.HasValue ? "change" : "reset",
            PasswordSecurity.CodeHash(id, code, Key), PasswordSecurity.Stamp(user.PasswordHash, Key), DateTime.UtcNow.AddMinutes(10));
        if (!await challenges.CreateAsync(challenge, user.PasswordHash, cancellationToken))
        {
            if (userId.HasValue) throw new PasswordCooldownException();
            return response;
        }
        try { await email.SendCodeAsync(user.Email, code, cancellationToken); }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            // Do not log addresses, provider responses, passwords or verification codes.
            logger.LogWarning("Password verification email delivery failed.");
            if (userId.HasValue) throw new PasswordEmailUnavailableException();
        }
        return response;
    }

    public async Task CompleteAsync(Guid challengeId, Guid? userId, string code, string newPassword, string confirmPassword, CancellationToken cancellationToken)
    {
        PasswordSecurity.ValidatePassword(newPassword);
        if (newPassword != confirmPassword) throw new ArgumentException("Passwords do not match.");
        if (code.Length != 6 || code.Any(c => c is < '0' or > '9')) throw new ArgumentException("Enter the six-digit verification code.");
        var hash = BCrypt.Net.BCrypt.HashPassword(newPassword);
        if (!await challenges.CompleteAsync(challengeId, userId, userId.HasValue ? "change" : "reset", code, hash, Key, cancellationToken))
            throw new ArgumentException("The code is invalid, expired or already used. Request a new code.");
    }
}
public sealed class PasswordEmailUnavailableException : Exception;
public sealed class PasswordCooldownException : Exception;
