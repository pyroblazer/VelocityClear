using FinancialPlatform.EventInfrastructure.Bus;
using FinancialPlatform.Shared.Enums;
using FinancialPlatform.Shared.Events;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace FinancialPlatform.UnitTests.Services;

public class AdaptiveEventBusTests
{
    private AdaptiveEventBus CreateBus()
    {
        return new AdaptiveEventBus(
            Mock.Of<ILogger<AdaptiveEventBus>>(),
            Mock.Of<ILogger<InMemoryEventBus>>(),
            Mock.Of<ILogger<RedisEventBus>>(),
            Mock.Of<ILogger<RabbitMQEventBus>>(),
            Mock.Of<ILogger<KafkaEventBus>>());
    }

    [Fact]
    public void Constructor_StartsWithInMemoryBackend()
    {
        var bus = CreateBus();
        Assert.Equal(EventBusBackend.InMemory, bus.CurrentBackend);
    }

    [Fact]
    public async Task PublishAsync_DeliversEvent_ToSubscriber()
    {
        var bus = CreateBus();
        TransactionCreatedEvent? captured = null;
        await bus.SubscribeAsync<TransactionCreatedEvent>(e => { captured = e; return Task.CompletedTask; });

        var evt = new TransactionCreatedEvent("tx_1", "user_1", 100m, "USD", DateTime.UtcNow);
        await bus.PublishAsync(evt);

        Assert.NotNull(captured);
        Assert.Equal("tx_1", captured.TransactionId);
    }

    [Fact]
    public async Task PublishAsync_DeliversMultipleEventTypes()
    {
        var bus = CreateBus();
        TransactionCreatedEvent? txEvent = null;
        RiskEvaluatedEvent? riskEvent = null;

        await bus.SubscribeAsync<TransactionCreatedEvent>(e => { txEvent = e; return Task.CompletedTask; });
        await bus.SubscribeAsync<RiskEvaluatedEvent>(e => { riskEvent = e; return Task.CompletedTask; });

        await bus.PublishAsync(new TransactionCreatedEvent("tx_1", "user_1", 100m, "USD", DateTime.UtcNow));
        await bus.PublishAsync(new RiskEvaluatedEvent("tx_1", 50, "MEDIUM", [], DateTime.UtcNow));

        Assert.NotNull(txEvent);
        Assert.NotNull(riskEvent);
    }

    [Fact]
    public async Task SubscribeAsync_MultipleHandlers_AllReceiveEvent()
    {
        var bus = CreateBus();
        var count = 0;
        await bus.SubscribeAsync<TransactionCreatedEvent>(_ => { Interlocked.Increment(ref count); return Task.CompletedTask; });
        await bus.SubscribeAsync<TransactionCreatedEvent>(_ => { Interlocked.Increment(ref count); return Task.CompletedTask; });

        await bus.PublishAsync(new TransactionCreatedEvent("tx_1", "user_1", 100m, "USD", DateTime.UtcNow));
        Assert.Equal(2, count);
    }

    [Fact]
    public async Task PublishAsync_WithNoSubscriber_DoesNotThrow()
    {
        var bus = CreateBus();
        await bus.PublishAsync(new TransactionCreatedEvent("tx_1", "user_1", 100m, "USD", DateTime.UtcNow));
    }

    [Fact]
    public async Task StartAsync_DoesNotThrow()
    {
        var bus = CreateBus();
        await bus.StartAsync();
        await bus.StopAsync();
    }

    [Fact]
    public void BackendChanged_EventCanBeSubscribedTo()
    {
        var bus = CreateBus();
        EventBusBackend? changedTo = null;
        bus.BackendChanged += (_, backend) => changedTo = backend;
        // Event subscription should not throw
        Assert.Null(changedTo);
    }

    [Fact]
    public async Task StopAsync_WithoutStart_DoesNotThrow()
    {
        var bus = CreateBus();
        await bus.StopAsync();
    }
}
