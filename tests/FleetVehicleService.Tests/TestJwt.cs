using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace FleetVehicleService.Tests;

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