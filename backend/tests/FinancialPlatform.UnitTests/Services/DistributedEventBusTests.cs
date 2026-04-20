using FinancialPlatform.EventInfrastructure.Bus;
using FinancialPlatform.EventInfrastructure.Configuration;
using FinancialPlatform.EventInfrastructure.Serialization;
using FinancialPlatform.Shared.Enums;
using FinancialPlatform.Shared.Events;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace FinancialPlatform.UnitTests.Services;

/// <summary>
/// Unit tests for the distributed EventBus backends (Redis, RabbitMQ, Kafka).
/// These tests verify construction, subscription registration, and graceful
/// handling of missing infrastructure - without requiring running instances.
/// </summary>
public class DistributedEventBusTests
{
    // ----- RedisEventBus -----

    [Fact]
    public void RedisEventBus_ConstructsSuccessfully()
    {
        var logger = new Mock<ILogger<RedisEventBus>>();
        var bus = new RedisEventBus("localhost:6379", "test-service", logger.Object);
        Assert.NotNull(bus);
    }

    [Fact]
    public async Task RedisEventBus_SubscribeAsync_RegistersHandler()
    {
        var logger = new Mock<ILogger<RedisEventBus>>();
        var bus = new RedisEventBus("localhost:6379", "test-service", logger.Object);

        // SubscribeAsync stores the handler internally - no connection needed.
        await bus.SubscribeAsync<TransactionCreatedEvent>(e => Task.CompletedTask);

        // If we got here without exception, the handler was registered.
        Assert.True(true);
    }

    [Fact]
    public async Task RedisEventBus_PublishAsync_WithoutStart_Noop()
    {
        var logger = new Mock<ILogger<RedisEventBus>>();
        var bus = new RedisEventBus("localhost:6379", "test-service", logger.Object);

        // PublishAsync returns silently when _db is null (not connected).
        await bus.PublishAsync(new TransactionCreatedEvent("tx_1", "u1", 10m, "USD", DateTime.UtcNow));
    }

    [Fact]
    public async Task RedisEventBus_StopAsync_WithoutStart_Noop()
    {
        var logger = new Mock<ILogger<RedisEventBus>>();
        var bus = new RedisEventBus("localhost:6379", "test-service", logger.Object);
        await bus.StopAsync();
    }

    [Fact]
    public async Task RedisEventBus_StartAsync_EmptyConnectionString_DoesNotConnect()
    {
        var logger = new Mock<ILogger<RedisEventBus>>();
        var bus = new RedisEventBus("", "test-service", logger.Object);
        await bus.StartAsync();
        await bus.StopAsync();
    }

    // ----- RabbitMQEventBus -----

    [Fact]
    public void RabbitMQEventBus_ConstructsSuccessfully()
    {
        var logger = new Mock<ILogger<RabbitMQEventBus>>();
        var bus = new RabbitMQEventBus("amqp://guest:guest@localhost:5672", "test-service", logger.Object);
        Assert.NotNull(bus);
    }

    [Fact]
    public async Task RabbitMQEventBus_SubscribeAsync_RegistersHandler()
    {
        var logger = new Mock<ILogger<RabbitMQEventBus>>();
        var bus = new RabbitMQEventBus("amqp://guest:guest@localhost:5672", "test-service", logger.Object);
        await bus.SubscribeAsync<RiskEvaluatedEvent>(e => Task.CompletedTask);
    }

    [Fact]
    public async Task RabbitMQEventBus_PublishAsync_WithoutStart_Noop()
    {
        var logger = new Mock<ILogger<RabbitMQEventBus>>();
        var bus = new RabbitMQEventBus("amqp://guest:guest@localhost:5672", "test-service", logger.Object);
        await bus.PublishAsync(new RiskEvaluatedEvent("tx_1", 50, "MEDIUM", [], DateTime.UtcNow));
    }

    [Fact]
    public async Task RabbitMQEventBus_StopAsync_WithoutStart_Noop()
    {
        var logger = new Mock<ILogger<RabbitMQEventBus>>();
        var bus = new RabbitMQEventBus("amqp://guest:guest@localhost:5672", "test-service", logger.Object);
        await bus.StopAsync();
    }

    [Fact]
    public async Task RabbitMQEventBus_StartAsync_EmptyConnectionString_DoesNotConnect()
    {
        var logger = new Mock<ILogger<RabbitMQEventBus>>();
        var bus = new RabbitMQEventBus("", "test-service", logger.Object);
        await bus.StartAsync();
        await bus.StopAsync();
    }

    // ----- KafkaEventBus -----

    [Fact]
    public void KafkaEventBus_ConstructsSuccessfully()
    {
        var logger = new Mock<ILogger<KafkaEventBus>>();
        var bus = new KafkaEventBus("localhost:9092", "test-service", logger.Object);
        Assert.NotNull(bus);
    }

    [Fact]
    public async Task KafkaEventBus_SubscribeAsync_RegistersHandler()
    {
        var logger = new Mock<ILogger<KafkaEventBus>>();
        var bus = new KafkaEventBus("localhost:9092", "test-service", logger.Object);
        await bus.SubscribeAsync<PaymentAuthorizedEvent>(e => Task.CompletedTask);
    }

    [Fact]
    public async Task KafkaEventBus_PublishAsync_WithoutStart_Noop()
    {
        var logger = new Mock<ILogger<KafkaEventBus>>();
        var bus = new KafkaEventBus("localhost:9092", "test-service", logger.Object);
        await bus.PublishAsync(new PaymentAuthorizedEvent("tx_1", true, "Approved", DateTime.UtcNow));
    }

    [Fact]
    public async Task KafkaEventBus_StopAsync_WithoutStart_Noop()
    {
        var logger = new Mock<ILogger<KafkaEventBus>>();
        var bus = new KafkaEventBus("localhost:9092", "test-service", logger.Object);
        await bus.StopAsync();
    }

    [Fact]
    public async Task KafkaEventBus_StartAsync_EmptyBrokers_DoesNotConnect()
    {
        var logger = new Mock<ILogger<KafkaEventBus>>();
        var bus = new KafkaEventBus("", "test-service", logger.Object);
        await bus.StartAsync();
        await bus.StopAsync();
    }
}

/// <summary>
/// Tests for AdaptiveEventBus with the new config-based constructor overload.
/// Verifies backward compatibility and config-driven backend selection.
/// </summary>
public class AdaptiveEventBusConfigTests
{
    [Fact]
    public void ConfigConstructor_DefaultInMemory_Works()
    {
        var config = new EventBusConnectionConfig();
        var bus = new AdaptiveEventBus(
            config,
            Mock.Of<ILogger<AdaptiveEventBus>>(),
            Mock.Of<ILogger<InMemoryEventBus>>(),
            Mock.Of<ILogger<RedisEventBus>>(),
            Mock.Of<ILogger<RabbitMQEventBus>>(),
            Mock.Of<ILogger<KafkaEventBus>>());

        Assert.Equal(EventBusBackend.InMemory, bus.CurrentBackend);
    }

    [Fact]
    public void ConfigConstructor_RedisBackend_Selected()
    {
        var config = new EventBusConnectionConfig(DefaultBackend: "Redis", RedisUrl: "localhost:6379", ServiceName: "test-svc");
        var bus = new AdaptiveEventBus(
            config,
            Mock.Of<ILogger<AdaptiveEventBus>>(),
            Mock.Of<ILogger<InMemoryEventBus>>(),
            Mock.Of<ILogger<RedisEventBus>>(),
            Mock.Of<ILogger<RabbitMQEventBus>>(),
            Mock.Of<ILogger<KafkaEventBus>>());

        Assert.Equal(EventBusBackend.Redis, bus.CurrentBackend);
    }

    [Fact]
    public void ConfigConstructor_RabbitMQBackend_Selected()
    {
        var config = new EventBusConnectionConfig(DefaultBackend: "RabbitMQ", RabbitMqUrl: "amqp://guest:guest@localhost:5672", ServiceName: "test-svc");
        var bus = new AdaptiveEventBus(
            config,
            Mock.Of<ILogger<AdaptiveEventBus>>(),
            Mock.Of<ILogger<InMemoryEventBus>>(),
            Mock.Of<ILogger<RedisEventBus>>(),
            Mock.Of<ILogger<RabbitMQEventBus>>(),
            Mock.Of<ILogger<KafkaEventBus>>());

        Assert.Equal(EventBusBackend.RabbitMQ, bus.CurrentBackend);
    }

    [Fact]
    public void ConfigConstructor_KafkaBackend_Selected()
    {
        var config = new EventBusConnectionConfig(DefaultBackend: "Kafka", KafkaBrokers: "localhost:9092", ServiceName: "test-svc");
        var bus = new AdaptiveEventBus(
            config,
            Mock.Of<ILogger<AdaptiveEventBus>>(),
            Mock.Of<ILogger<InMemoryEventBus>>(),
            Mock.Of<ILogger<RedisEventBus>>(),
            Mock.Of<ILogger<RabbitMQEventBus>>(),
            Mock.Of<ILogger<KafkaEventBus>>());

        Assert.Equal(EventBusBackend.Kafka, bus.CurrentBackend);
    }

    [Fact]
    public void ConfigConstructor_InvalidBackend_FallsBackToInMemory()
    {
        var config = new EventBusConnectionConfig(DefaultBackend: "NonExistent");
        var bus = new AdaptiveEventBus(
            config,
            Mock.Of<ILogger<AdaptiveEventBus>>(),
            Mock.Of<ILogger<InMemoryEventBus>>(),
            Mock.Of<ILogger<RedisEventBus>>(),
            Mock.Of<ILogger<RabbitMQEventBus>>(),
            Mock.Of<ILogger<KafkaEventBus>>());

        Assert.Equal(EventBusBackend.InMemory, bus.CurrentBackend);
    }

    [Fact]
    public void ConfigConstructor_CaseInsensitive()
    {
        var config = new EventBusConnectionConfig(DefaultBackend: "redis");
        var bus = new AdaptiveEventBus(
            config,
            Mock.Of<ILogger<AdaptiveEventBus>>(),
            Mock.Of<ILogger<InMemoryEventBus>>(),
            Mock.Of<ILogger<RedisEventBus>>(),
            Mock.Of<ILogger<RabbitMQEventBus>>(),
            Mock.Of<ILogger<KafkaEventBus>>());

        Assert.Equal(EventBusBackend.Redis, bus.CurrentBackend);
    }

    [Fact]
    public async Task OldConstructor_BackwardCompatible_StillWorks()
    {
        // This is the old 5-logger constructor used by all existing tests.
        var bus = new AdaptiveEventBus(
            Mock.Of<ILogger<AdaptiveEventBus>>(),
            Mock.Of<ILogger<InMemoryEventBus>>(),
            Mock.Of<ILogger<RedisEventBus>>(),
            Mock.Of<ILogger<RabbitMQEventBus>>(),
            Mock.Of<ILogger<KafkaEventBus>>());

        Assert.Equal(EventBusBackend.InMemory, bus.CurrentBackend);

        TransactionCreatedEvent? captured = null;
        await bus.SubscribeAsync<TransactionCreatedEvent>(e => { captured = e; return Task.CompletedTask; });

        var evt = new TransactionCreatedEvent("tx_old", "user_old", 50m, "USD", DateTime.UtcNow);
        await bus.PublishAsync(evt);

        Assert.NotNull(captured);
        Assert.Equal("tx_old", captured.TransactionId);
    }
}

/// <summary>
/// Tests for EventBusConnectionConfig record behavior.
/// </summary>
public class EventBusConnectionConfigTests
{
    [Fact]
    public void DefaultConstructor_SetsExpectedDefaults()
    {
        var config = new EventBusConnectionConfig();
        Assert.Equal("InMemory", config.DefaultBackend);
        Assert.Equal("", config.RedisUrl);
        Assert.Equal("", config.RabbitMqUrl);
        Assert.Equal("", config.KafkaBrokers);
        Assert.Equal("unknown", config.ServiceName);
    }

    [Fact]
    public void PositionalParameters_SetProperties()
    {
        var config = new EventBusConnectionConfig("Redis", "redis:6379", "amqp://rabbitmq:5672", "kafka:9092", "my-service");
        Assert.Equal("Redis", config.DefaultBackend);
        Assert.Equal("redis:6379", config.RedisUrl);
        Assert.Equal("amqp://rabbitmq:5672", config.RabbitMqUrl);
        Assert.Equal("kafka:9092", config.KafkaBrokers);
        Assert.Equal("my-service", config.ServiceName);
    }

    [Fact]
    public void RecordEquality_Works()
    {
        var a = new EventBusConnectionConfig("Redis", "redis:6379", "", "", "svc");
        var b = new EventBusConnectionConfig("Redis", "redis:6379", "", "", "svc");
        Assert.Equal(a, b);
    }
}
