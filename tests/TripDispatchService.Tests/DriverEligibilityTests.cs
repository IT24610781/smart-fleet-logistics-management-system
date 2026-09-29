using System.Net;
using System.Text;
using System.Text.Json;
using TripDispatchService.Services;

namespace TripDispatchService.Tests;

public class DriverEligibilityTests
{
    private static readonly Guid Id = Guid.NewGuid();

    [Theory]
    [InlineData(true, "Driver", true)]
    [InlineData(false, "Driver", false)]
    [InlineData(true, "Admin", false)]
    public async Task OnlyActiveDriverCanBeAssigned(bool active, string role, bool allowed)
    {
        using var factory = new Factory(JsonSerializer.Serialize(new[] { new { id = Id, role, isActive = active } }));
        var guard = new DriverEligibilityClient(factory);
        if (allowed) await guard.EnsureActiveAsync(Id, "Bearer dispatcher", default);
        else await Assert.ThrowsAsync<InvalidOperationException>(() => guard.EnsureActiveAsync(Id, "Bearer dispatcher", default));
    }

    [Fact]
    public async Task MissingDriverIsRejected()
    {
        using var factory = new Factory("[]");
        await Assert.ThrowsAsync<InvalidOperationException>(() => new DriverEligibilityClient(factory).EnsureActiveAsync(Id, "Bearer dispatcher", default));
    }

    [Theory]
    [InlineData("null", 200)]
    [InlineData("invalid json", 200)]
    [InlineData("[]", 503)]
    public async Task DependencyFailurePreventsAssignment(string body, int status)
    {
        using var factory = new Factory(body, (HttpStatusCode)status);
        await Assert.ThrowsAsync<DriverEligibilityUnavailableException>(() => new DriverEligibilityClient(factory).EnsureActiveAsync(Id, "Bearer dispatcher", default));
    }

    private sealed class Factory(string body, HttpStatusCode status = HttpStatusCode.OK) : HttpMessageHandler, IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(this, disposeHandler: false) { BaseAddress = new Uri("http://auth/") };
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            Assert.Equal("/api/users/drivers", request.RequestUri!.AbsolutePath);
            Assert.Equal("dispatcher", request.Headers.Authorization?.Parameter);
            return Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") });
        }
    }
}
