using System.Security.Cryptography;
using System.Text;

namespace AuthUserService.Services;

public static class PasswordSecurity
{
    public static string Stamp(string passwordHash, string key) => Convert.ToHexString(
        HMACSHA256.HashData(Encoding.UTF8.GetBytes(key), Encoding.UTF8.GetBytes(passwordHash)));

    public static string CodeHash(Guid challenge, string code, string key) => Stamp($"{challenge}:{code}", key);

    public static bool Matches(string expected, string actual) => CryptographicOperations.FixedTimeEquals(
        Encoding.UTF8.GetBytes(expected), Encoding.UTF8.GetBytes(actual));

    public static string MaskEmail(string email)
    {
        var parts = email.Split('@');
        if (parts.Length != 2) return "***";
        // Never reveal the complete local part, including short email addresses.
        var suffix = parts[0].Length > 2 ? parts[0][^2..] : "";
        return $"***{suffix}@{parts[1]}";
    }

    public static void ValidatePassword(string password)
    {
        if (password.Length < 8 || Encoding.UTF8.GetByteCount(password) > 72)
            throw new ArgumentException("Use at least 8 characters and no more than 72 UTF-8 bytes for your password.");
    }
}
