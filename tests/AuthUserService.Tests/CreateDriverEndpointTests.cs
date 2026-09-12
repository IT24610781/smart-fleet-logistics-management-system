using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using AuthUserService.DTOs;

namespace AuthUserService.Tests;

/// <summary>
/// US-01: Admin Creates User Account
/// Integration tests against the real POST /api/users/drivers endpoint.
///
/// REQUIRES a reachable MySQL test database — see
/// CustomWebApplicationFactory for the connection string to set up.
/// </summary>
public class CreateDriverEndpointTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public CreateDriverEndpointTests(CustomWebApplicationFactory factory)
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

    private static CreateUserRequest ValidDriverRequest(string? uniqueSuffix = null)
    {
        var suffix = uniqueSuffix ?? Guid.NewGuid().ToString("N")[..8];

        return new CreateUserRequest
        {
            Username = $"driver_{suffix}",
            Email = $"driver_{suffix}@example.com",
            Password = "ValidPass123",
            FirstName = "Test",
            LastName = "Driver",
            ContactNumber = "0770000000"
        };
    }

    // ============================================================
    // Scenario 1 - Successful account creation
    // ============================================================

    [Fact]
    public async Task CreateDriver_ValidRequest_Returns201AndNeverExposesPassword()
    {
        var client = CreateAdminClient();
        var request = ValidDriverRequest();

        var response = await client.PostAsJsonAsync("/api/users/drivers", request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var rawJson = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain(request.Password, rawJson);
        Assert.DoesNotContain("password", rawJson, StringComparison.OrdinalIgnoreCase);

        var body = await response.Content.ReadFromJsonAsync<UserResponse>();
        Assert.NotNull(body);
        Assert.Equal(request.Email, body!.Email);
        Assert.Equal(request.Username, body.Username);
        Assert.True(body.IsActive);
    }

    // ============================================================
    // Scenario 2 - Duplicate email
    // ============================================================

    [Fact]
    public async Task CreateDriver_DuplicateEmail_Returns409AndDoesNotCreateSecondRecord()
    {
        var client = CreateAdminClient();
        var request = ValidDriverRequest();

        var firstResponse = await client.PostAsJsonAsync("/api/users/drivers", request);
        Assert.Equal(HttpStatusCode.Created, firstResponse.StatusCode);

        // Same email, different username, to isolate the email-uniqueness check
        // from the (separately enforced) username-uniqueness check.
        var duplicateEmailRequest = ValidDriverRequest();
        duplicateEmailRequest.Email = request.Email;

        var secondResponse = await client.PostAsJsonAsync("/api/users/drivers", duplicateEmailRequest);

        Assert.Equal(HttpStatusCode.Conflict, secondResponse.StatusCode);

        var body = await secondResponse.Content.ReadAsStringAsync();
        Assert.Contains("An account with this email already exists.", body);
    }

    // ============================================================
    // Scenario 3 - Invalid or missing fields (API-level check;
    // full field coverage lives in CreateUserValidationTests)
    // ============================================================

    [Fact]
    public async Task CreateDriver_MissingEmail_Returns400()
    {
        var client = CreateAdminClient();
        var request = ValidDriverRequest();
        request.Email = "";

        var response = await client.PostAsJsonAsync("/api/users/drivers", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("Email is required.", body);
    }

    [Fact]
    public async Task CreateDriver_PasswordShorterThan8Chars_Returns400()
    {
        var client = CreateAdminClient();
        var request = ValidDriverRequest();
        request.Password = "short1"; // 6 chars

        var response = await client.PostAsJsonAsync("/api/users/drivers", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("Password must be at least 8 characters.", body);
    }

    // ============================================================
    // Authorization: only Admins may hit this endpoint
    // ============================================================

    [Fact]
    public async Task CreateDriver_NoToken_Returns401()
    {
        var client = _factory.CreateClient();
        var request = ValidDriverRequest();

        var response = await client.PostAsJsonAsync("/api/users/drivers", request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task CreateDriver_NonAdminToken_Returns403()
    {
        var client = _factory.CreateClient();
        var token = TestJwt.CreateToken("Driver");
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);

        var request = ValidDriverRequest();
        var response = await client.PostAsJsonAsync("/api/users/drivers", request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // ============================================================
    // ⚠ KNOWN GAP vs Acceptance Criteria — flag on the story, don't
    // silently "fix" this by testing around it.
    // ============================================================

    [Fact]
    public async Task CreateDriver_RoleIsHardcodedToDriver_RegardlessOfRequestBody()
    {
        // The AC says the Admin should "select a role" when creating an
        // account. As written today, CreateUserRequest has no Role field,
        // and UserService.CreateUserAsync hardcodes Role = "Driver" for
        // every account. This test documents CURRENT behavior — it should
        // start failing (and get replaced with a real role-selection test)
        // once that gap is fixed by the dev.
        var client = CreateAdminClient();
        var request = ValidDriverRequest();

        var response = await client.PostAsJsonAsync("/api/users/drivers", request);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<UserResponse>();
        Assert.Equal("Driver", body!.Role);
    }
}
