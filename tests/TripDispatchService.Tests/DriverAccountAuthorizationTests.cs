using System.Net;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using TripDispatchService.Services;

namespace TripDispatchService.Tests;

public class DriverAccountAuthorizationTests
{
    private static readonly Guid Id = Guid.NewGuid();

    [Theory]
    [InlineData(true, "Driver", false)]
    [InlineData(false, "Driver", true)]
    [InlineData(true, "Admin", true)]
    public async Task DriverTokenRequiresMatchingActiveAccount(bool active, string role, bool denied)
    {
        var body = JsonSerializer.Serialize(new { id = Id, role, isActive = active });
        var context = Context(body);
        await AccountTokenEvents.Create().OnTokenValidated(context);
        Assert.Equal(denied, context.Result?.Failure != null);
    }

    [Fact]
    public async Task AccountIdentityMismatchIsRejected()
    {
        var context = Context(JsonSerializer.Serialize(new { id = Guid.NewGuid(), role = "Driver", isActive = true }));
        await AccountTokenEvents.Create().OnTokenValidated(context);
        Assert.NotNull(context.Result?.Failure);
    }

    [Theory]
    [InlineData(401)]
    [InlineData(403)]
    [InlineData(404)]
    public async Task RevokedOrMissingAccountIsRejected(int status)
    {
        var context = Context("{}", (HttpStatusCode)status);
        await AccountTokenEvents.Create().OnTokenValidated(context);
        Assert.NotNull(context.Result?.Failure);
        Assert.False(context.HttpContext.Items.ContainsKey("DriverStatusUnavailable"));
    }

    [Theory]
    [InlineData("{}", 503)]
    [InlineData("invalid json", 200)]
    public async Task DependencyFailureFailsClosedWith503(string body, int status)
    {
        var context = Context(body, (HttpStatusCode)status);
        var events = AccountTokenEvents.Create();
        await events.OnTokenValidated(context);
        Assert.NotNull(context.Result?.Failure);
        var challenge = new JwtBearerChallengeContext(context.HttpContext, context.Scheme, context.Options, new AuthenticationProperties());
        context.Response.Body = new MemoryStream();
        await events.OnChallenge(challenge);
        Assert.True(challenge.Handled);
        Assert.Equal(503, context.Response.StatusCode);
    }

    [Theory]
    [InlineData("Admin")]
    [InlineData("Dispatcher")]
    public async Task AllRolesRequireReachableAuthForSessionValidation(string role)
    {
        var context = Context("invalid", HttpStatusCode.ServiceUnavailable, role);
        await AccountTokenEvents.Create().OnTokenValidated(context);
        Assert.NotNull(context.Result?.Failure);
        Assert.True(context.HttpContext.Items.ContainsKey("DriverStatusUnavailable"));
    }

    [Theory]
    [InlineData("Admin")]
    [InlineData("Dispatcher")]
    public async Task ActiveMatchingAccountIsAcceptedForEveryRole(string role)
    {
        var context = Context(JsonSerializer.Serialize(new { id = Id, role, isActive = true }), HttpStatusCode.OK, role);
        await AccountTokenEvents.Create().OnTokenValidated(context);
        Assert.Null(context.Result);
    }

    private static TokenValidatedContext Context(string body, HttpStatusCode status = HttpStatusCode.OK, string role = "Driver")
    {
        var http = new DefaultHttpContext();
        var services = new ServiceCollection();
        services.AddSingleton<IHttpClientFactory>(new Factory(body, status));
        http.RequestServices = services.BuildServiceProvider();
        http.Request.Headers.Authorization = "Bearer test";
        return new TokenValidatedContext(http, new AuthenticationScheme("Bearer", null, typeof(JwtBearerHandler)), new JwtBearerOptions())
        {
            Principal = new ClaimsPrincipal(new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, Id.ToString()), new Claim(ClaimTypes.Role, role)
            }, "Bearer"))
        };
    }

    private sealed class Factory(string body, HttpStatusCode status) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(new Handler(body, status)) { BaseAddress = new Uri("http://auth/") };
    }
    private sealed class Handler(string body, HttpStatusCode status) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            Assert.Equal("/api/users/me", request.RequestUri!.AbsolutePath);
            Assert.Equal("Bearer", request.Headers.Authorization?.Scheme);
            return Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") });
        }
    }
}
