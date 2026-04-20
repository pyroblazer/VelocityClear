using FinancialPlatform.EventInfrastructure.Bus;
using FinancialPlatform.RiskService.Services;
using FinancialPlatform.Shared.Events;
using FinancialPlatform.Shared.Interfaces;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace FinancialPlatform.UnitTests.Services;

public class RiskEvaluationServiceTests
{
    private readonly Mock<ILogger<RiskEvaluationService>> _loggerMock;

    public RiskEvaluationServiceTests()
    {
        _loggerMock = new Mock<ILogger<RiskEvaluationService>>();
    }

    private async Task<(AdaptiveEventBus Bus, RiskEvaluatedEvent? Result)> EvaluateWithCapture(TransactionCreatedEvent evt)
    {
        var bus = new AdaptiveEventBus(
            Mock.Of<ILogger<AdaptiveEventBus>>(),
            Mock.Of<ILogger<InMemoryEventBus>>(),
            Mock.Of<ILogger<RedisEventBus>>(),
            Mock.Of<ILogger<RabbitMQEventBus>>(),
            Mock.Of<ILogger<KafkaEventBus>>());

        RiskEvaluatedEvent? captured = null;
        await bus.SubscribeAsync<RiskEvaluatedEvent>(e => { captured = e; return Task.CompletedTask; });

        var service = new RiskEvaluationService(bus, _loggerMock.Object);
        await service.EvaluateAsync(evt);

        return (bus, captured);
    }

    [Fact]
    public async Task SmallAmount_ReturnsLowRisk()
    {
        var (_, result) = await EvaluateWithCapture(new TransactionCreatedEvent(
            "tx_001", "user_001", 100m, "USD", new DateTime(2026, 4, 19, 14, 0, 0, DateTimeKind.Utc)));

        Assert.NotNull(result);
        Assert.Equal("tx_001", result.TransactionId);
        Assert.True(result.RiskScore < 50);
        Assert.Equal("LOW", result.RiskLevel);
    }

    [Fact]
    public async Task LargeAmount_ReturnsElevatedScore()
    {
        var (_, result) = await EvaluateWithCapture(new TransactionCreatedEvent(
            "tx_002", "user_002", 15000m, "USD", new DateTime(2026, 4, 19, 14, 0, 0, DateTimeKind.Utc)));

        Assert.NotNull(result);
        Assert.True(result.RiskScore >= 50);
        Assert.Contains("HIGH_AMOUNT", result.Flags);
    }

    [Fact]
    public async Task OddHour_AddsOddHourFlag()
    {
        var (_, result) = await EvaluateWithCapture(new TransactionCreatedEvent(
            "tx_003", "user_003", 500m, "USD", new DateTime(2026, 4, 19, 3, 0, 0, DateTimeKind.Utc)));

        Assert.NotNull(result);
        Assert.Contains("ODD_HOUR", result.Flags);
    }

    [Fact]
    public async Task AmountOver5K_ScoresAtLeast30()
    {
        var (_, result) = await EvaluateWithCapture(new TransactionCreatedEvent(
            "tx_004", "user_004", 7500m, "USD", new DateTime(2026, 4, 19, 12, 0, 0, DateTimeKind.Utc)));

        Assert.NotNull(result);
        Assert.True(result.RiskScore >= 30);
    }

    [Fact]
    public async Task SmallDaytime_NoFlags()
    {
        var (_, result) = await EvaluateWithCapture(new TransactionCreatedEvent(
            "tx_005", "user_005", 50m, "EUR", new DateTime(2026, 4, 19, 10, 0, 0, DateTimeKind.Utc)));

        Assert.NotNull(result);
        Assert.Empty(result.Flags);
        Assert.Equal(0, result.RiskScore);
    }

    [Fact]
    public async Task VeryLargeAmount_ScoresAtLeast50()
    {
        var (_, result) = await EvaluateWithCapture(new TransactionCreatedEvent(
            "tx_006", "user_006", 20000m, "USD", new DateTime(2026, 4, 19, 12, 0, 0, DateTimeKind.Utc)));

        Assert.NotNull(result);
        Assert.True(result.RiskScore >= 50);
        Assert.Equal("MEDIUM", result.RiskLevel);
    }

    [Fact]
    public async Task MultipleFlags_Combines()
    {
        var (_, result) = await EvaluateWithCapture(new TransactionCreatedEvent(
            "tx_007", "user_007", 12000m, "USD", new DateTime(2026, 4, 19, 3, 0, 0, DateTimeKind.Utc)));

        Assert.NotNull(result);
        Assert.True(result.RiskScore >= 65);
        Assert.Contains("HIGH_AMOUNT", result.Flags);
        Assert.Contains("ODD_HOUR", result.Flags);
    }
}
