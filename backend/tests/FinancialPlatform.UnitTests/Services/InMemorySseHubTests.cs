using System.Text;
using FinancialPlatform.EventInfrastructure.Sse;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace FinancialPlatform.UnitTests.Services;

public class InMemorySseHubTests
{
    private InMemorySseHub CreateHub()
    {
        return new InMemorySseHub(Mock.Of<ILogger<InMemorySseHub>>());
    }

    private HttpResponse CreateMockResponse(StringWriter writer)
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        context.Response.StatusCode = 200;
        return context.Response;
    }

    [Fact]
    public void ActiveConnections_StartsAtZero()
    {
        var hub = CreateHub();
        Assert.Equal(0, hub.ActiveConnections);
    }

    [Fact]
    public void AddClient_IncrementsActiveConnections()
    {
        var hub = CreateHub();
        var response = CreateMockResponse(null!);
        hub.AddClient("client_1", response);
        Assert.Equal(1, hub.ActiveConnections);
    }

    [Fact]
    public void AddClient_MultipleClients_TrackedCorrectly()
    {
        var hub = CreateHub();
        var response1 = CreateMockResponse(null!);
        var response2 = CreateMockResponse(null!);
        var response3 = CreateMockResponse(null!);

        hub.AddClient("c1", response1);
        hub.AddClient("c2", response2);
        hub.AddClient("c3", response3);

        Assert.Equal(3, hub.ActiveConnections);
    }

    [Fact]
    public void RemoveClient_DecrementsActiveConnections()
    {
        var hub = CreateHub();
        var response = CreateMockResponse(null!);
        hub.AddClient("client_1", response);
        hub.RemoveClient("client_1");
        Assert.Equal(0, hub.ActiveConnections);
    }

    [Fact]
    public void RemoveClient_UnknownClient_DoesNotThrow()
    {
        var hub = CreateHub();
        hub.RemoveClient("nonexistent");
        Assert.Equal(0, hub.ActiveConnections);
    }

    [Fact]
    public void AddClient_SameIdReplaced_ConnectionCountUnchanged()
    {
        var hub = CreateHub();
        var response1 = CreateMockResponse(null!);
        var response2 = CreateMockResponse(null!);
        hub.AddClient("client_1", response1);
        hub.AddClient("client_1", response2);
        Assert.Equal(1, hub.ActiveConnections);
    }

    [Fact]
    public async Task BroadcastAsync_WithNoClients_DoesNotThrow()
    {
        var hub = CreateHub();
        await hub.BroadcastAsync("TestEvent", new { message = "hello" });
    }
}
