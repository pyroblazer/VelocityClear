using FinancialPlatform.EventInfrastructure.Bus;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace FinancialPlatform.UnitTests.Services;

public class InMemoryEventBusTests
{
    [Fact]
    public async Task ShouldDeliverEvent_ToSubscribedHandler()
    {
        var logger = new Mock<ILogger<InMemoryEventBus>>();
        var bus = new InMemoryEventBus(logger.Object);
        var received = new List<string>();

        await bus.SubscribeAsync<string>(async msg =>
        {
            received.Add(msg);
            await Task.CompletedTask;
        });

        await bus.PublishAsync("hello");

        Assert.Single(received);
        Assert.Equal("hello", received[0]);
    }

    [Fact]
    public async Task ShouldDeliverToMultipleHandlers()
    {
        var logger = new Mock<ILogger<InMemoryEventBus>>();
        var bus = new InMemoryEventBus(logger.Object);
        var count = 0;

        await bus.SubscribeAsync<string>(async msg => { count++; await Task.CompletedTask; });
        await bus.SubscribeAsync<string>(async msg => { count++; await Task.CompletedTask; });

        await bus.PublishAsync("test");

        Assert.Equal(2, count);
    }

    [Fact]
    public async Task ShouldNotFail_WhenNoHandlers()
    {
        var logger = new Mock<ILogger<InMemoryEventBus>>();
        var bus = new InMemoryEventBus(logger.Object);

        await bus.PublishAsync("orphan event");

        Assert.True(true);
    }
}
