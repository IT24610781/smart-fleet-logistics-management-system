using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Collections.Concurrent;
using AuthUserService.Services;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MySqlConnector;

namespace AuthUserService.Tests;

public class PasswordSecurityTests
{
    [Theory]
    [InlineData("driver67@gmail.com", "***67@gmail.com")]
    [InlineData("a@example.com", "***@example.com")]
    [InlineData("ab@example.com", "***@example.com")]
    public void EmailMaskNeverRevealsFullLocalPart(string email, string expected) => Assert.Equal(expected, PasswordSecurity.MaskEmail(email));

    [Fact]
    public void CodeHashIsBoundToChallengeAndKey()
    {
        var id = Guid.NewGuid();
        var hash = PasswordSecurity.CodeHash(id, "123456", "secret");
        Assert.False(PasswordSecurity.Matches(hash, PasswordSecurity.CodeHash(Guid.NewGuid(), "123456", "secret")));
        Assert.False(PasswordSecurity.Matches(hash, PasswordSecurity.CodeHash(id, "123456", "other")));
        Assert.True(PasswordSecurity.Matches(hash, PasswordSecurity.CodeHash(id, "123456", "secret")));
    }

    [Theory]
    [InlineData("short")]
    [InlineData("界界界界界界界界界界界界界界界界界界界界界界界界界")]
    public void PasswordPolicyProtectsBcryptByteLimit(string password) => Assert.Throws<ArgumentException>(() => PasswordSecurity.ValidatePassword(password));
}

// Runs only against the isolated test schema configured by CustomWebApplicationFactory.
public class PasswordRecoveryTests(CustomWebApplicationFactory factory) : IClassFixture<CustomWebApplicationFactory>
{
    private sealed class Inbox : IPasswordEmailSender
    {
        public bool IsConfigured => true;
        public ConcurrentDictionary<string, string> Codes { get; } = new();
        public Task SendCodeAsync(string email, string code, CancellationToken ct) { Codes[email] = code; return Task.CompletedTask; }
    }

    private async Task<(HttpClient Client, Inbox Mail, string Email, string Username, Guid UserId, string Token)> Setup()
    {
        var mail = new Inbox();
        var app = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IPasswordEmailSender>();
            services.AddSingleton<IPasswordEmailSender>(mail);
        }));
        var client = app.CreateClient();
        var suffix = Guid.NewGuid().ToString("N")[..12];
        var email = $"pw{suffix}@example.com";
        var username = $"pw{suffix}";
        var register = await client.PostAsJsonAsync("/api/auth/register/dispatcher", new
        {
            username, email, password = "Original-Password123!", firstName = "Password", lastName = "Test", contactNumber = "+94760000000"
        });
        Assert.Equal(HttpStatusCode.Created, register.StatusCode);
        var user = await register.Content.ReadFromJsonAsync<UserBody>();
        var login = await client.PostAsJsonAsync("/api/auth/login", new { username, password = "Original-Password123!" });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var result = await login.Content.ReadFromJsonAsync<LoginBody>();
        return (client, mail, email, username, user!.Id, result!.Token);
    }
    private sealed record UserBody(Guid Id);
    private sealed record LoginBody(string Token);
    private sealed record CodeBody(Guid ChallengeId, string MaskedEmail);
    private static object Complete(Guid id, string code) => new { challengeId = id, code, newPassword = "Replacement-Password123!", confirmPassword = "Replacement-Password123!" };

    [Fact]
    public async Task ResetRequiresCodeRevokesOldSessionAndCodeCannotBeReused()
    {
        var s = await Setup();
        using var client = s.Client;
        var request = await client.PostAsJsonAsync("/api/auth/password/forgot", new { email = s.Email });
        Assert.Equal(HttpStatusCode.OK, request.StatusCode);
        var body = await request.Content.ReadFromJsonAsync<CodeBody>();
        Assert.Equal(PasswordSecurity.MaskEmail(s.Email), body!.MaskedEmail);
        Assert.DoesNotContain(s.Mail.Codes[s.Email], await request.Content.ReadAsStringAsync());
        var reset = await client.PostAsJsonAsync("/api/auth/password/reset", Complete(body.ChallengeId, s.Mail.Codes[s.Email]));
        Assert.Equal(HttpStatusCode.OK, reset.StatusCode);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", s.Token);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/users/me")).StatusCode);
        client.DefaultRequestHeaders.Authorization = null;
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/auth/login", new { username = s.Username, password = "Original-Password123!" })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/auth/login", new { username = s.Username, password = "Replacement-Password123!" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/auth/password/reset", Complete(body.ChallengeId, s.Mail.Codes[s.Email]))).StatusCode);
    }

    [Fact]
    public async Task FiveWrongCodesBlockEvenTheCorrectCode()
    {
        var s = await Setup();
        using var client = s.Client;
        var request = await client.PostAsJsonAsync("/api/auth/password/forgot", new { email = s.Email });
        var body = await request.Content.ReadFromJsonAsync<CodeBody>();
        var wrong = s.Mail.Codes[s.Email] == "000000" ? "000001" : "000000";
        for (var i = 0; i < 5; i++)
            Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/auth/password/reset", Complete(body!.ChallengeId, wrong))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/auth/password/reset", Complete(body!.ChallengeId, s.Mail.Codes[s.Email]))).StatusCode);
    }

    [Fact]
    public async Task ExpiredCodeIsRejected()
    {
        var s = await Setup();
        using var client = s.Client;
        var request = await client.PostAsJsonAsync("/api/auth/password/forgot", new { email = s.Email });
        var body = await request.Content.ReadFromJsonAsync<CodeBody>();
        await using var connection = new MySqlConnection(CustomWebApplicationFactory.EffectiveConnectionString);
        await connection.OpenAsync();
        await using var command = new MySqlCommand("UPDATE password_challenges SET expires_at=UTC_TIMESTAMP()-INTERVAL 1 MINUTE WHERE id=@id", connection);
        command.Parameters.AddWithValue("@id", body!.ChallengeId.ToString());
        await command.ExecuteNonQueryAsync();
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/auth/password/reset", Complete(body.ChallengeId, s.Mail.Codes[s.Email]))).StatusCode);
    }

    [Fact]
    public async Task ChangeRequiresCurrentPasswordAndCannotUseResetEndpoint()
    {
        var s = await Setup();
        using var client = s.Client;
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/auth/password/change/request", new { currentPassword = "Original-Password123!" })).StatusCode);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", s.Token);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/auth/password/change/request", new { currentPassword = "Wrong-password" })).StatusCode);
        var request = await client.PostAsJsonAsync("/api/auth/password/change/request", new { currentPassword = "Original-Password123!" });
        Assert.Equal(HttpStatusCode.OK, request.StatusCode);
        var body = await request.Content.ReadFromJsonAsync<CodeBody>();
        Assert.Equal(HttpStatusCode.TooManyRequests, (await client.PostAsJsonAsync("/api/auth/password/change/request", new { currentPassword = "Original-Password123!" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/auth/password/reset", Complete(body!.ChallengeId, s.Mail.Codes[s.Email]))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/auth/password/change/confirm", Complete(body.ChallengeId, s.Mail.Codes[s.Email]))).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/users/me")).StatusCode);
    }

    [Fact]
    public async Task UnknownEmailGetsSamePublicResponseWithoutSendingMail()
    {
        var s = await Setup();
        using var client = s.Client;
        var unknown = "unknown" + Guid.NewGuid().ToString("N") + "@example.com";
        var result = await client.PostAsJsonAsync("/api/auth/password/forgot", new { email = unknown });
        Assert.Equal(HttpStatusCode.OK, result.StatusCode);
        Assert.False(s.Mail.Codes.ContainsKey(unknown));
        Assert.Contains("If this email belongs to an active account", await result.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task ProfileCannotBypassCodeVerification()
    {
        var s = await Setup();
        using var client = s.Client;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", s.Token);
        var update = await client.PutAsJsonAsync("/api/users/me", new { firstName = "Password", lastName = "Test", contactNumber = "+94760000000", password = "Bypass-Password123!" });
        Assert.Equal(HttpStatusCode.BadRequest, update.StatusCode);
    }

    [Fact]
    public async Task ConcurrentConfirmationsConsumeCodeOnlyOnceAndRevokeLegacyToken()
    {
        var s = await Setup();
        using var client = s.Client;
        var request = await client.PostAsJsonAsync("/api/auth/password/forgot", new { email = s.Email });
        var body = await request.Content.ReadFromJsonAsync<CodeBody>();
        var payload = Complete(body!.ChallengeId, s.Mail.Codes[s.Email]);
        var results = await Task.WhenAll(client.PostAsJsonAsync("/api/auth/password/reset", payload), client.PostAsJsonAsync("/api/auth/password/reset", payload));
        Assert.Single(results, response => response.StatusCode == HttpStatusCode.OK);
        Assert.Single(results, response => response.StatusCode == HttpStatusCode.BadRequest);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TestJwt.CreateToken("Dispatcher", subject: s.UserId.ToString()));
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/users/me")).StatusCode);
    }

    [Fact]
    public async Task CodeCannotChangeAnotherSignedInAccount()
    {
        var s = await Setup();
        using var client = s.Client;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", s.Token);
        var request = await client.PostAsJsonAsync("/api/auth/password/change/request", new { currentPassword = "Original-Password123!" });
        var body = await request.Content.ReadFromJsonAsync<CodeBody>();
        var other = await Setup();
        using var otherClient = other.Client;
        otherClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", other.Token);
        Assert.Equal(HttpStatusCode.BadRequest, (await otherClient.PostAsJsonAsync("/api/auth/password/change/confirm", Complete(body!.ChallengeId, s.Mail.Codes[s.Email]))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/auth/password/change/confirm", Complete(body.ChallengeId, s.Mail.Codes[s.Email]))).StatusCode);
    }
}
