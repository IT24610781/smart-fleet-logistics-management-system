using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using AuthUserService.DTOs;

namespace AuthUserService.Tests;

/// <summary>
/// US-03: User Logout
///
/// IMPORTANT CONTEXT FOR ANYONE READING THIS FILE:
/// This story has NO backend logic. Confirmed by reading the code:
///   - AuthController exposes only POST /api/auth/login — there is no
///     /api/auth/logout endpoint.
///   - There is no token blacklist, session table, or any server-side
///     revocation mechanism anywhere in AuthUserService.
///   - The frontend's logout() (AuthContext.jsx) only does
///     localStorage.removeItem() for the token/user and resets React
///     state — it never calls the backend at all.
///
/// This is a legitimate design choice for a stateless JWT system (the
/// backend doesn't need to "know" about logout), but it means the
/// AC's guarantee — "I can no longer access protected routes until I
/// log in again" — is enforced entirely on the CLIENT (by no longer
/// sending a token), not by the server rejecting that specific token.
///
/// These tests document that reality at the backend layer: they don't
/// test "logout" (there's nothing to call), they confirm the
/// assumptions the frontend's logout behavior relies on, and they
/// document the one real trade-off worth knowing about.
/// </summary>
public class LogoutArchitectureTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public LogoutArchitectureTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task NoLogoutEndpointExists_ConfirmsLogoutIsClientSideOnly()
    {
        // There is genuinely no POST /api/auth/logout route. This test
        // documents that fact rather than assuming it — if a logout
        // endpoint gets added later, this test should start failing
        // (404 -> something else), which is the signal to replace it
        // with a real test of that endpoint's behavior.
        var client = _factory.CreateClient();

        var response = await client.PostAsync("/api/auth/logout", content: null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task ProtectedRoute_WithNoToken_Returns401()
    {
        // This is what "logged out" looks like from the backend's
        // perspective: once the frontend stops sending a token (which
        // is all logout actually does), the server correctly rejects
        // the request. This overlaps with US-02's equivalent test —
        // included here too since it's the concrete backend guarantee
        // this story's AC is actually relying on.
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/users/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task RealUserToken_StillAuthenticatesAfterConceptualLogout_NoServerSideRevocation()
    {
        // ⚠ KNOWN ARCHITECTURAL LIMITATION — documented, not a bug to
        // "fix" without a design discussion first.
        //
        // "Logout" only clears the token from the browser's
        // localStorage (see AuthContext.jsx). The JWT itself is never
        // revoked server-side. This test proves that: log in for real,
        // "log out" (client-side only — nothing to call on the
        // backend), then replay the exact same token. It still
        // authenticates successfully.
        //
        // Practical implication: if a token were ever copied out of
        // the browser before logout (dev tools, a malicious script, a
        // proxy log), it keeps working for the rest of its natural
        // lifetime regardless of the user clicking "Logout". This is a
        // standard trade-off of stateless JWTs, not unique to this
        // codebase — but worth the team consciously deciding on (e.g.
        // short token lifetimes, or a future denylist) rather than
        // assuming logout provides real server-side revocation.
        var adminClient = _factory.CreateClient();
        var adminToken = TestJwt.CreateToken("Admin");
        adminClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", adminToken);

        var suffix = Guid.NewGuid().ToString("N")[..8];
        var createRequest = new CreateUserRequest
        {
            Username = $"logouttest_{suffix}",
            Email = $"logouttest_{suffix}@example.com",
            Password = "ValidPass123",
            FirstName = "Logout",
            LastName = "Test",
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
        var login = await loginResponse.Content.ReadFromJsonAsync<LoginResponse>();

        // "User clicks Logout" here — client-side only, nothing to
        // call on the backend. We simply keep using the same token,
        // exactly as a captured/leaked token would behave.
        var replayClient = _factory.CreateClient();
        replayClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", login!.Token);

        var replayResponse = await replayClient.GetAsync("/api/users/me");

        // This passing assertion IS the finding: the token still
        // works. There is no backend-side "you logged out" state.
        Assert.Equal(HttpStatusCode.OK, replayResponse.StatusCode);
    }
}
