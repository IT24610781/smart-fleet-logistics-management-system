using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using AuthUserService.DTOs;

namespace AuthUserService.Tests;

/// <summary>
/// US-08: Dispatcher Creates Own Account
/// Integration tests against the real POST /api/auth/register/dispatcher endpoint.
///
/// REQUIRES a reachable MySQL test database - see
/// CustomWebApplicationFactory for the connection string to set up.
/// </summary>
public class RegisterDispatcherEndpointTests : IClassFixture<CustomWebApplicationFactory>
{
    private const string RegisterUrl = "/api/auth/register/dispatcher";

    private readonly CustomWebApplicationFactory _factory;

    public RegisterDispatcherEndpointTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    // Registration is public, so the client deliberately has NO token.
    private HttpClient CreateAnonymousClient() => _factory.CreateClient();

    private static CreateUserRequest ValidRequest(string? uniqueSuffix = null)
    {
        var suffix = uniqueSuffix ?? Guid.NewGuid().ToString("N")[..8];

        return new CreateUserRequest
        {
            Username = $"disp_{suffix}",
            Email = $"disp_{suffix}@example.com",
            Password = "ValidPass123",
            FirstName = "Test",
            LastName = "Dispatcher",
            ContactNumber = "0771234567"
        };
    }

    // ============================================================
    // Scenario 1 - Successful registration
    // ============================================================

    [Fact]
    public async Task Register_ValidRequest_Returns201WithDispatcherRoleAndNoPassword()
    {
        var client = CreateAnonymousClient();
        var request = ValidRequest();

        var response = await client.PostAsJsonAsync(RegisterUrl, request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var rawJson = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain(request.Password, rawJson);
        Assert.DoesNotContain("password", rawJson, StringComparison.OrdinalIgnoreCase);

        var body = JsonSerializer.Deserialize<UserResponse>(
            rawJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        Assert.NotNull(body);
        Assert.Equal("Dispatcher", body!.Role);
        Assert.Equal(request.Username, body.Username);
        Assert.True(body.IsActive);
    }

    [Fact]
    public async Task Register_BodyContainsRoleAdmin_StillCreatesDispatcher()
    {
        // Security check: a caller must never be able to pick their own role.
        var client = CreateAnonymousClient();
        var suffix = Guid.NewGuid().ToString("N")[..8];

        var response = await client.PostAsJsonAsync(RegisterUrl, new
        {
            username = $"sneaky_{suffix}",
            email = $"sneaky_{suffix}@example.com",
            password = "ValidPass123",
            firstName = "Sneaky",
            lastName = "User",
            contactNumber = "0771234567",
            role = "Admin"
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<UserResponse>();
        Assert.Equal("Dispatcher", body!.Role);
    }

    // ============================================================
    // Scenario 2 - Missing or invalid fields
    // ============================================================

    [Fact]
    public async Task Register_AllFieldsMissing_Returns400ListingEveryField()
    {
        var client = CreateAnonymousClient();

        var response = await client.PostAsJsonAsync(RegisterUrl, new CreateUserRequest());

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();
        foreach (var field in new[]
                 { "username", "email", "password", "firstName", "lastName", "contactNumber" })
        {
            Assert.Contains($"\"{field}\"", body);
        }
    }

    [Fact]
    public async Task Register_InvalidEmailAndShortPassword_Returns400WithBothFieldErrors()
    {
        var client = CreateAnonymousClient();
        var request = ValidRequest();
        request.Email = "not-an-email";
        request.Password = "short";

        var response = await client.PostAsJsonAsync(RegisterUrl, request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("Enter a valid email address.", body);
        Assert.Contains("Password must be at least 8 characters.", body);
    }

    [Fact]
    public async Task Register_OversizedFields_Returns400NotRaw500()
    {
        // Same class of bug as SCRUM-86: input longer than the DB column
        // must be rejected by validation, not by MySQL.
        var client = CreateAnonymousClient();
        var request = ValidRequest();
        request.FirstName = new string('A', 101);
        request.ContactNumber = new string('1', 21);

        var response = await client.PostAsJsonAsync(RegisterUrl, request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ============================================================
    // Scenario 3 - Duplicate username or email
    // ============================================================

    [Fact]
    public async Task Register_DuplicateUsername_Returns409()
    {
        var client = CreateAnonymousClient();
        var first = ValidRequest();
        Assert.Equal(HttpStatusCode.Created,
            (await client.PostAsJsonAsync(RegisterUrl, first)).StatusCode);

        var second = ValidRequest();
        second.Username = first.Username;

        var response = await client.PostAsJsonAsync(RegisterUrl, second);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Contains("Username already exists.", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Register_DuplicateEmail_Returns409()
    {
        var client = CreateAnonymousClient();
        var first = ValidRequest();
        Assert.Equal(HttpStatusCode.Created,
            (await client.PostAsJsonAsync(RegisterUrl, first)).StatusCode);

        var second = ValidRequest();
        second.Email = first.Email;

        var response = await client.PostAsJsonAsync(RegisterUrl, second);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Contains("An account with this email already exists.",
            await response.Content.ReadAsStringAsync());
    }

    // ============================================================
    // Scenario 4 - Login after registration returns a Dispatcher JWT
    // ============================================================

    [Fact]
    public async Task Register_ThenLogin_ReturnsTokenWithDispatcherRole()
    {
        var client = CreateAnonymousClient();
        var request = ValidRequest();
        Assert.Equal(HttpStatusCode.Created,
            (await client.PostAsJsonAsync(RegisterUrl, request)).StatusCode);

        var loginResponse = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest
        {
            Username = request.Username,
            Password = request.Password
        });

        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);

        var login = await loginResponse.Content.ReadFromJsonAsync<LoginResponse>();
        Assert.False(string.IsNullOrWhiteSpace(login!.Token));
        Assert.Equal("Dispatcher", login.User.Role);
    }

    // ============================================================
    // Scenario 5 - A Dispatcher cannot use Admin-only functionality
    // ============================================================

    [Fact]
    public async Task RegisteredDispatcher_CallingAdminOnlyEndpoint_Returns403()
    {
        var client = CreateAnonymousClient();
        var request = ValidRequest();
        await client.PostAsJsonAsync(RegisterUrl, request);

        var loginResponse = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest
        {
            Username = request.Username,
            Password = request.Password
        });
        var login = await loginResponse.Content.ReadFromJsonAsync<LoginResponse>();

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", login!.Token);

        var adminOnly = await client.PostAsJsonAsync("/api/users/drivers", ValidRequest());

        Assert.Equal(HttpStatusCode.Forbidden, adminOnly.StatusCode);
    }
}
