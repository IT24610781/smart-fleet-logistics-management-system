using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using AuthUserService.DTOs;
using MySqlConnector;

namespace AuthUserService.Tests;

/// <summary>
/// US-02: User Login with JWT
/// Integration tests against the real POST /api/auth/login endpoint
/// and a protected route (GET /api/users/me), covering all 3 AC
/// scenarios end-to-end.
///
/// REQUIRES a reachable MySQL test database — see
/// CustomWebApplicationFactory for the connection string to set up.
/// Reuses the same test database as US-01's CreateDriverEndpointTests.
/// </summary>
public class LoginEndpointTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public LoginEndpointTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private HttpClient CreateAdminClient()
    {
        var client = _factory.CreateClient();
        var token = TestJwt.CreateToken("Admin");
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    /// <summary>
    /// Creates a real driver account through the actual API (as US-01
    /// does), so login tests exercise real stored credentials rather
    /// than hand-crafted DB rows. Returns (username, password).
    /// </summary>
    private async Task<(string Username, string Password)> CreateTestDriverAsync()
    {
        var client = CreateAdminClient();
        var suffix = Guid.NewGuid().ToString("N")[..8];

        var request = new CreateUserRequest
        {
            Username = $"logintest_{suffix}",
            Email = $"logintest_{suffix}@example.com",
            Password = "ValidPass123",
            FirstName = "Login",
            LastName = "Test",
            ContactNumber = "0770000000"
        };

        var response = await client.PostAsJsonAsync("/api/users/drivers", request);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        return (request.Username, request.Password);
    }

    // ============================================================
    // Scenario 1 - Successful login
    // ============================================================

    [Fact]
    public async Task Login_ValidCredentials_Returns200WithJwtContainingUserIdAndRole()
    {
        var (username, password) = await CreateTestDriverAsync();
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest
        {
            Username = username,
            Password = password
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<LoginResponse>();
        Assert.NotNull(body);
        Assert.False(string.IsNullOrWhiteSpace(body!.Token));
        Assert.Equal(username, body.User.Username);
        Assert.Equal("Driver", body.User.Role);

        // Decode the JWT and verify it actually carries user ID + role,
        // per the AC: "the API returns a signed JWT containing my user
        // ID and role."
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(body.Token);
        var subClaim = jwt.Claims.FirstOrDefault(c => c.Type == "sub")?.Value;
        var roleClaim = jwt.Claims.FirstOrDefault(c =>
            c.Type == "role" || c.Type.EndsWith("/role"))?.Value;

        Assert.Equal(body.User.Id.ToString(), subClaim);
        Assert.Equal("Driver", roleClaim);
    }

    // ============================================================
    // Scenario 2 - Invalid credentials
    // ============================================================

    [Fact]
    public async Task Login_WrongPassword_Returns401WithGenericMessage()
    {
        var (username, _) = await CreateTestDriverAsync();
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest
        {
            Username = username,
            Password = "TotallyWrongPassword"
        });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();

        // NOTE: the AC specifies the message "Invalid email or password."
        // The actual current implementation returns "Invalid username or
        // password." instead. This test asserts the REAL current message
        // so the suite reflects actual behavior — see the write-up on
        // this gap (login is username-based, not email-based, at all).
        Assert.Contains("Invalid username or password.", body);
    }

    [Fact]
    public async Task Login_NonExistentUsername_Returns401WithSameGenericMessage()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest
        {
            Username = $"does_not_exist_{Guid.NewGuid():N}",
            Password = "whatever123"
        });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("Invalid username or password.", body);
    }

    [Fact]
    public async Task Login_WrongPasswordAndNonExistentUsername_ReturnIdenticalMessages()
    {
        // Confirms the DoD requirement "no user enumeration" holds for
        // THIS specific pair of cases (wrong password vs unknown user).
        var (username, _) = await CreateTestDriverAsync();
        var client = _factory.CreateClient();

        var wrongPasswordResponse = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest
        {
            Username = username,
            Password = "TotallyWrongPassword"
        });
        var unknownUserResponse = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest
        {
            Username = $"does_not_exist_{Guid.NewGuid():N}",
            Password = "whatever123"
        });

        var wrongPasswordBody = await wrongPasswordResponse.Content.ReadAsStringAsync();
        var unknownUserBody = await unknownUserResponse.Content.ReadAsStringAsync();

        Assert.Equal(wrongPasswordBody, unknownUserBody);
    }

    // ============================================================
    // Scenario 3 - Protected route access
    // ============================================================

    [Fact]
    public async Task ProtectedRoute_ValidToken_Returns200()
    {
        var (username, password) = await CreateTestDriverAsync();
        var client = _factory.CreateClient();

        var loginResponse = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest
        {
            Username = username,
            Password = password
        });
        var login = await loginResponse.Content.ReadFromJsonAsync<LoginResponse>();

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", login!.Token);

        var response = await client.GetAsync("/api/users/me");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<UserResponse>();
        Assert.Equal(username, body!.Username);
    }

    [Fact]
    public async Task ProtectedRoute_NoToken_Returns401()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/users/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ProtectedRoute_TamperedToken_Returns401()
    {
        var client = _factory.CreateClient();
        var token = TestJwt.CreateToken("Driver");

        // Flip a character near the end of the signature segment.
        var tampered = token[..^1] + (token[^1] == 'A' ? 'B' : 'A');

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", tampered);

        var response = await client.GetAsync("/api/users/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ProtectedRoute_ExpiredToken_Returns401()
    {
        var client = _factory.CreateClient();
        var expiredToken = TestJwt.CreateToken(
            "Driver",
            expiresIn: TimeSpan.FromMinutes(-5));

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", expiredToken);

        var response = await client.GetAsync("/api/users/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // ============================================================
    // ⚠ KNOWN GAPS vs Acceptance Criteria — flagged, not silently
    // worked around. See accompanying bug write-ups.
    // ============================================================

    [Fact]
    public async Task Login_ByEmailInsteadOfUsername_DoesNotWork()
    {
        // The AC says the user logs in with "email and password", but
        // LoginRequest has no Email field at all — only Username. This
        // test documents that sending an email-shaped value in the
        // Username field does NOT match any real username and fails
        // login, proving email-based login is not actually supported.
        var (_, password) = await CreateTestDriverAsync();
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest
        {
            Username = "logintest_doesnotexist@example.com", // an email, not a username
            Password = password
        });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Login_InactiveAccount_ReturnsDifferentMessage_PotentialEnumerationLeak()
    {
        // DoD explicitly requires "no user enumeration". This test
        // documents that an inactive account currently returns a
        // DIFFERENT message ("This account is inactive.") than an
        // unknown/wrong-credentials login ("Invalid username or
        // password."). That difference lets an attacker distinguish
        // "account exists but disabled" from "account doesn't exist",
        // which is itself a form of user enumeration.
        var (username, password) = await CreateTestDriverAsync();

        // Deactivate the account directly in the DB — there's no API
        // path to do this yet.
        await using (var connection = new MySqlConnection(CustomWebApplicationFactory.TestConnectionString))
        {
            await connection.OpenAsync();
            await using var command = new MySqlCommand(
                "UPDATE users SET is_active = 0 WHERE username = @username",
                connection);
            command.Parameters.AddWithValue("@username", username);
            await command.ExecuteNonQueryAsync();
        }

        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest
        {
            Username = username,
            Password = password
        });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("This account is inactive.", body);
    }
}
