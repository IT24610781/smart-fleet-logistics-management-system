using System.Net;
using System.Text;
using TripDispatchService.Services;
using Xunit;

namespace TripDispatchService.Tests;

public class FleetVehicleClientFailureTests
{
    [Fact]
    public async Task Suggestions_FleetError_ReportsDependencyUnavailable()
    {
        var client = CreateClient(_ => new HttpResponseMessage(HttpStatusCode.InternalServerError));

        await Assert.ThrowsAsync<FleetVehicleUnavailableException>(() => client.GetVehiclesAsync(null));
    }

    [Fact]
    public async Task Suggestions_InvalidJson_ReportsDependencyUnavailable()
    {
        var client = CreateClient(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("not-json", Encoding.UTF8, "application/json")
        });

        await Assert.ThrowsAsync<FleetVehicleUnavailableException>(() => client.GetVehiclesAsync(null));
    }

    [Fact]
    public async Task Assignment_FleetTimeout_ReportsDependencyUnavailable()
    {
        var client = CreateClient(_ => throw new TaskCanceledException("request timed out"));

        await Assert.ThrowsAsync<FleetVehicleUnavailableException>(() =>
            client.SetVehicleStatusAsync(Guid.NewGuid(), "On Trip", null));
    }

    private static FleetVehicleClient CreateClient(Func<HttpRequestMessage, HttpResponseMessage> respond)
    {
        var http = new HttpClient(new Handler(respond))
        {
            BaseAddress = new Uri("https://fleet.example.test")
        };
        return new FleetVehicleClient(http);
    }

    private sealed class Handler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(respond(request));
    }
}
