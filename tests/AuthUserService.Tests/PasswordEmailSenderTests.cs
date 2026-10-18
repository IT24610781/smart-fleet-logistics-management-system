using System.Net;
using System.Text.Json;
using AuthUserService.Services;
using Microsoft.Extensions.Configuration;

namespace AuthUserService.Tests;

[Trait("Category", "Unit")]
public class PasswordEmailSenderTests
{
    private const string TestKey = "test-only-api-key";
    private const string Sender = "sender@example.test";

    [Theory]
    [InlineData(null, Sender)]
    [InlineData("", Sender)]
    [InlineData("   ", Sender)]
    [InlineData(TestKey, null)]
    [InlineData(TestKey, "")]
    [InlineData(TestKey, "   ")]
    public async Task MissingConfiguration_RejectsBeforeMakingHttpRequest(string? key, string? sender)
    {
        using var handler = new RecordingHandler(HttpStatusCode.Created);
        using var http = new HttpClient(handler);
        var service = new PasswordEmailSender(http, Config(key, sender));

        Assert.False(service.IsConfigured);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.SendCodeAsync("driver@example.test", "123456", default));

        Assert.Equal(0, handler.Calls);
    }

    [Theory]
    [InlineData("driver@example.test", "123456")]
    [InlineData("dispatcher@example.test", "000042")]
    public async Task SendCode_SendsExactRecipientAndCodeOnce(string recipient, string code)
    {
        // HttpClient runs normally; the handler intercepts the request before any network I/O.
        using var handler = new RecordingHandler(HttpStatusCode.Created);
        using var http = new HttpClient(handler);
        var service = new PasswordEmailSender(http, Config(TestKey, Sender));

        Assert.True(service.IsConfigured);
        await service.SendCodeAsync(recipient, code, default);

        Assert.Equal(1, handler.Calls);
        Assert.Equal(HttpMethod.Post, handler.Method);
        Assert.Equal("https://api.brevo.com/v3/smtp/email", handler.Url);
        Assert.Equal(TestKey, handler.ApiKey);
        using var json = JsonDocument.Parse(handler.Body!);
        var root = json.RootElement;
        Assert.Equal(Sender, root.GetProperty("sender").GetProperty("email").GetString());
        var recipients = root.GetProperty("to").EnumerateArray().ToArray();
        Assert.Single(recipients);
        Assert.Equal(recipient, recipients[0].GetProperty("email").GetString());
        Assert.Contains(code, root.GetProperty("textContent").GetString());
        Assert.DoesNotContain(TestKey, handler.Body!);
    }

    [Theory]
    [InlineData(401)]
    [InlineData(429)]
    [InlineData(500)]
    [InlineData(503)]
    public async Task ProviderFailure_IsReportedToCallerWithoutImplicitRetry(int status)
    {
        using var handler = new RecordingHandler((HttpStatusCode)status);
        using var http = new HttpClient(handler);
        var service = new PasswordEmailSender(http, Config(TestKey, Sender));

        var error = await Assert.ThrowsAsync<HttpRequestException>(() =>
            service.SendCodeAsync("driver@example.test", "123456", default));

        Assert.Equal((HttpStatusCode)status, error.StatusCode);
        Assert.Equal(1, handler.Calls);
    }

    private static IConfiguration Config(string? key, string? sender) =>
        new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["PasswordEmail:ApiKey"] = key,
            ["PasswordEmail:SenderEmail"] = sender
        }).Build();

    // Stub: supplies a response. Spy: captures the request for interaction assertions.
    private sealed class RecordingHandler(HttpStatusCode status) : HttpMessageHandler
    {
        public int Calls { get; private set; }
        public HttpMethod? Method { get; private set; }
        public string? Url { get; private set; }
        public string? ApiKey { get; private set; }
        public string? Body { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            Method = request.Method;
            Url = request.RequestUri!.AbsoluteUri;
            ApiKey = request.Headers.GetValues("api-key").Single();
            Body = await request.Content!.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(status);
        }
    }
}
