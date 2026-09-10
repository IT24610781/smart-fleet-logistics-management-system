using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace AuthUserService.Tests;

/// <summary>
/// Generates JWTs signed with the same test key used by
/// CustomWebApplicationFactory, so tests can act as a given role
/// (e.g. "Admin") without going through the real /api/auth/login flow.
///
/// UPDATED for US-02: added an optional `expiresIn` parameter so tests
/// can mint an already-expired token (pass a negative TimeSpan) to
/// verify AC Scenario 3 - expired tokens must be rejected.
/// </summary>
public static class TestJwt
{
    public static string CreateToken(
        string role,
        string username = "testuser",
        string email = "testuser@example.com",
        string? subject = null,
        TimeSpan? expiresIn = null)
    {
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, subject ?? Guid.NewGuid().ToString()),
            new(JwtRegisteredClaimNames.UniqueName, username),
            new(JwtRegisteredClaimNames.Email, email),
            new(ClaimTypes.Role, role)
        };

        var key = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(CustomWebApplicationFactory.JwtTestKey));

        var credentials = new SigningCredentials(
            key, SecurityAlgorithms.HmacSha256);

        var expires = DateTime.UtcNow.Add(expiresIn ?? TimeSpan.FromMinutes(30));

        var token = new JwtSecurityToken(
            issuer: CustomWebApplicationFactory.JwtTestIssuer,
            audience: CustomWebApplicationFactory.JwtTestAudience,
            claims: claims,
            expires: expires,
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
