using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using AuthUserService.DTOs;

namespace AuthUserService.Tests;

/// <summary>
/// US-04: View and Edit Profile
/// Integration tests against the real GET/PUT /api/users/me endpoints.
///
/// REQUIRES a reachable MySQL test database — see
/// CustomWebApplicationFactory. Reuses the same test database as the
/// other stories' endpoint tests.
///
/// NOTE: the frontend's userService.js and Profile.jsx currently have
/// comments claiming these endpoints are "NOT YET IMPLEMENTED BY THE
/// BACKEND — placeholder". That is stale — GET/PUT /api/users/me are
/// fully implemented (UsersController.GetMyProfile /
/// UpdateMyProfile). Worth flagging to whoever owns the frontend so
/// the placeholder error-handling branch (Profile.jsx's `err.status
/// === 404` case) gets cleaned up — it will simply never fire now.
/// </summary>
public class ProfileEndpointTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public ProfileEndpointTests(CustomWebApplicationFactory factory)
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
    /// Creates a real driver account and logs in for real, so profile
    /// tests exercise a real user id / real JWT rather than a
    /// hand-crafted one.
    /// </summary>
    private async Task<(string Username, string Password, LoginResponse Login)> CreateAndLoginTestDriverAsync()
    {
        var adminClient = CreateAdminClient();
        var suffix = Guid.NewGuid().ToString("N")[..8];

        var createRequest = new CreateUserRequest
        {
            Username = $"profiletest_{suffix}",
            Email = $"profiletest_{suffix}@example.com",
            Password = "ValidPass123",
            FirstName = "Original",
            LastName = "Name",
            ContactNumber = "0770000000"
        };

        var createResponse = await adminClient.PostAsJsonAsync("/api/users/drivers", createRequest);
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

        var loginClient = _factory.CreateClient();
        var loginResponse = await loginClient.PostAsJsonAsync("/api/auth/login", new LoginRequest
        {
            Username = createRequest.Username,
            Password = createRequest.Password
        });
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);
        var login = await loginResponse.Content.ReadFromJsonAsync<LoginResponse>();

        return (createRequest.Username, createRequest.Password, login!);
    }

    private HttpClient CreateAuthenticatedClient(string token)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    // ============================================================
    // Scenario 1 - View profile
    // ============================================================

    [Fact]
    public async Task GetProfile_ValidToken_ReturnsNameEmailAndContactNumber()
    {
        var (username, _, login) = await CreateAndLoginTestDriverAsync();
        var client = CreateAuthenticatedClient(login.Token);

        var response = await client.GetAsync("/api/users/me");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<UserResponse>();
        Assert.NotNull(body);
        Assert.Equal(username, body!.Username);
        Assert.Equal("Original", body.FirstName);
        Assert.Equal("Name", body.LastName);
        Assert.Equal("0770000000", body.ContactNumber);
        Assert.False(string.IsNullOrWhiteSpace(body.Email));
    }

    // ============================================================
    // Scenario 2 - Successful profile update
    // ============================================================

    [Fact]
    public async Task UpdateProfile_ValidNameAndContact_PersistsAndReflectsImmediately()
    {
        var (_, _, login) = await CreateAndLoginTestDriverAsync();
        var client = CreateAuthenticatedClient(login.Token);

        var updateRequest = new UpdateProfileRequest
        {
            FirstName = "Updated",
            LastName = "Person",
            ContactNumber = "0779999999"
        };

        var updateResponse = await client.PutAsJsonAsync("/api/users/me", updateRequest);
        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);

        // Reflected immediately in the update response itself.
        var updatedBody = await updateResponse.Content.ReadFromJsonAsync<UserResponse>();
        Assert.Equal("Updated", updatedBody!.FirstName);
        Assert.Equal("Person", updatedBody.LastName);
        Assert.Equal("0779999999", updatedBody.ContactNumber);

        // Persisted — confirmed via a fresh GET.
        var getResponse = await client.GetAsync("/api/users/me");
        var getBody = await getResponse.Content.ReadFromJsonAsync<UserResponse>();
        Assert.Equal("Updated", getBody!.FirstName);
        Assert.Equal("Person", getBody.LastName);
        Assert.Equal("0779999999", getBody.ContactNumber);
    }

    [Fact]
    public async Task UpdateProfile_EmailFieldIsNotEditable_NoEmailPropertyOnRequestDto()
    {
        // DoD: "Email address is read-only (not editable)". Confirmed
        // structurally — UpdateProfileRequest has no Email property at
        // all, so there's no way to send one through this endpoint
        // regardless of what a client attempts.
        var properties = typeof(UpdateProfileRequest).GetProperties();
        Assert.DoesNotContain(properties, p => p.Name == "Email");
    }

    // ============================================================
    // Scenario 3 - Invalid update
    // ============================================================

    [Fact]
    public async Task UpdateProfile_EmptyFirstName_Returns400AndDoesNotSaveChanges()
    {
        var (_, _, login) = await CreateAndLoginTestDriverAsync();
        var client = CreateAuthenticatedClient(login.Token);

        var updateRequest = new UpdateProfileRequest
        {
            FirstName = "",
            LastName = "StillValid",
            ContactNumber = "0770000001"
        };

        var response = await client.PutAsJsonAsync("/api/users/me", updateRequest);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("First name is required.", body);

        // Confirm nothing was saved — original values still in place.
        var getResponse = await client.GetAsync("/api/users/me");
        var getBody = await getResponse.Content.ReadFromJsonAsync<UserResponse>();
        Assert.Equal("Original", getBody!.FirstName);
        Assert.Equal("0770000000", getBody.ContactNumber);
    }

    // ============================================================
    // Scenario 4 - Password change
    // ============================================================

    [Fact]
    public async Task UpdateProfile_PasswordChangeWithoutVerification_IsRejected()
    {
        var (username, _, login) = await CreateAndLoginTestDriverAsync();
        var client = CreateAuthenticatedClient(login.Token);

        var updateRequest = new UpdateProfileRequest
        {
            FirstName = "Original",
            LastName = "Name",
            ContactNumber = "0770000000",
            Password = "NewValidPass456"
        };

        var updateResponse = await client.PutAsJsonAsync("/api/users/me", updateRequest);
        Assert.Equal(HttpStatusCode.BadRequest, updateResponse.StatusCode);
        Assert.Contains("verified Change password", await updateResponse.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/users/me")).StatusCode);
        var loginClient = _factory.CreateClient();
        Assert.Equal(HttpStatusCode.OK, (await loginClient.PostAsJsonAsync("/api/auth/login", new LoginRequest { Username = username, Password = "ValidPass123" })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await loginClient.PostAsJsonAsync("/api/auth/login", new LoginRequest { Username = username, Password = "NewValidPass456" })).StatusCode);
    }

    [Fact]
    public async Task UpdateProfile_PasswordLeftBlank_KeepsCurrentPassword()
    {
        var (username, password, login) = await CreateAndLoginTestDriverAsync();
        var client = CreateAuthenticatedClient(login.Token);

        var updateRequest = new UpdateProfileRequest
        {
            FirstName = "StillOriginal",
            LastName = "Name",
            ContactNumber = "0770000000"
            // Password intentionally omitted.
        };

        var updateResponse = await client.PutAsJsonAsync("/api/users/me", updateRequest);
        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);

        var loginClient = _factory.CreateClient();
        var loginResponse = await loginClient.PostAsJsonAsync("/api/auth/login", new LoginRequest
        {
            Username = username,
            Password = password // original password should still work
        });

        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);
    }

    // ============================================================
    // Auth requirement sanity checks
    // ============================================================

    [Fact]
    public async Task GetProfile_NoToken_Returns401()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/users/me");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task UpdateProfile_NoToken_Returns401()
    {
        var client = _factory.CreateClient();
        var response = await client.PutAsJsonAsync("/api/users/me", new UpdateProfileRequest
        {
            FirstName = "X",
            LastName = "Y",
            ContactNumber = "0770000000"
        });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
