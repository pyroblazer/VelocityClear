using FinancialPlatform.EventInfrastructure.Bus;
using FinancialPlatform.Shared.Events;
using FinancialPlatform.Shared.Interfaces;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace FinancialPlatform.UnitTests.Services;

public class PaymentServiceTests
{
    private async Task<(FinancialPlatform.PaymentService.Services.PaymentService Service, PaymentAuthorizedEvent? Result)>
        ProcessWithCapture(string txId, decimal amount, int riskScore)
    {
        var bus = new AdaptiveEventBus(
            Mock.Of<ILogger<AdaptiveEventBus>>(),
            Mock.Of<ILogger<InMemoryEventBus>>(),
            Mock.Of<ILogger<RedisEventBus>>(),
            Mock.Of<ILogger<RabbitMQEventBus>>(),
            Mock.Of<ILogger<KafkaEventBus>>());

        PaymentAuthorizedEvent? captured = null;
        await bus.SubscribeAsync<PaymentAuthorizedEvent>(e => { captured = e; return Task.CompletedTask; });

        var gateway = new FinancialPlatform.PaymentService.Services.PaymentGateway(
            Mock.Of<ILogger<FinancialPlatform.PaymentService.Services.PaymentGateway>>());

        var service = new FinancialPlatform.PaymentService.Services.PaymentService(
            bus, gateway, Mock.Of<ILogger<FinancialPlatform.PaymentService.Services.PaymentService>>());

        if (amount > 0)
            service.RegisterTransaction(txId, amount);

        await service.ProcessPaymentAsync(new RiskEvaluatedEvent(txId, riskScore, "LEVEL", [], DateTime.UtcNow));
        return (service, captured);
    }

    [Fact]
    public async Task LowRiskSmallAmount_Authorizes()
    {
        var (_, result) = await ProcessWithCapture("tx_001", 100m, 10);
        Assert.NotNull(result);
        Assert.True(result.Authorized);
        Assert.Equal("Authorized", result.Reason);
    }

    [Fact]
    public async Task HighRisk_Rejects()
    {
        var (_, result) = await ProcessWithCapture("tx_002", 1000m, 85);
        Assert.NotNull(result);
        Assert.False(result.Authorized);
    }

    [Fact]
    public async Task UnknownTransaction_ZeroAmount_Authorizes()
    {
        var (_, result) = await ProcessWithCapture("tx_unknown", 0m, 10);
        Assert.NotNull(result);
        Assert.True(result.Authorized);
    }

    [Fact]
    public async Task LargeAmountElevatedRisk_Rejects()
    {
        var (_, result) = await ProcessWithCapture("tx_003", 6000m, 60);
        Assert.NotNull(result);
        Assert.False(result.Authorized);
    }

    [Fact]
    public async Task PinVerificationFailed_Rejects()
    {
        var bus = new AdaptiveEventBus(
            Mock.Of<ILogger<AdaptiveEventBus>>(),
            Mock.Of<ILogger<InMemoryEventBus>>(),
            Mock.Of<ILogger<RedisEventBus>>(),
            Mock.Of<ILogger<RabbitMQEventBus>>(),
            Mock.Of<ILogger<KafkaEventBus>>());

        PaymentAuthorizedEvent? captured = null;
        await bus.SubscribeAsync<PaymentAuthorizedEvent>(e => { captured = e; return Task.CompletedTask; });

        var gateway = new FinancialPlatform.PaymentService.Services.PaymentGateway(
            Mock.Of<ILogger<FinancialPlatform.PaymentService.Services.PaymentGateway>>());

        var service = new FinancialPlatform.PaymentService.Services.PaymentService(
            bus, gateway, Mock.Of<ILogger<FinancialPlatform.PaymentService.Services.PaymentService>>());

        service.RegisterTransaction("tx_pin_fail", 500m);
        service.RecordPinVerification("tx_pin_fail", false);

        await service.ProcessPaymentAsync(new RiskEvaluatedEvent("tx_pin_fail", 10, "LOW", [], DateTime.UtcNow));

        Assert.NotNull(captured);
        Assert.False(captured.Authorized);
        Assert.Equal("PIN verification failed", captured.Reason);
    }

    [Fact]
    public async Task PinVerificationSucceeded_Authorizes()
    {
        var bus = new AdaptiveEventBus(
            Mock.Of<ILogger<AdaptiveEventBus>>(),
            Mock.Of<ILogger<InMemoryEventBus>>(),
            Mock.Of<ILogger<RedisEventBus>>(),
            Mock.Of<ILogger<RabbitMQEventBus>>(),
            Mock.Of<ILogger<KafkaEventBus>>());

        PaymentAuthorizedEvent? captured = null;
        await bus.SubscribeAsync<PaymentAuthorizedEvent>(e => { captured = e; return Task.CompletedTask; });

        var gateway = new FinancialPlatform.PaymentService.Services.PaymentGateway(
            Mock.Of<ILogger<FinancialPlatform.PaymentService.Services.PaymentGateway>>());

        var service = new FinancialPlatform.PaymentService.Services.PaymentService(
            bus, gateway, Mock.Of<ILogger<FinancialPlatform.PaymentService.Services.PaymentService>>());

        service.RegisterTransaction("tx_pin_ok", 200m);
        service.RecordPinVerification("tx_pin_ok", true);

        await service.ProcessPaymentAsync(new RiskEvaluatedEvent("tx_pin_ok", 5, "LOW", [], DateTime.UtcNow));

        Assert.NotNull(captured);
        Assert.True(captured.Authorized);
    }

    [Fact]
    public async Task RecordPinVerification_CleanedUpAfterProcessing()
    {
        var bus = new AdaptiveEventBus(
            Mock.Of<ILogger<AdaptiveEventBus>>(),
            Mock.Of<ILogger<InMemoryEventBus>>(),
            Mock.Of<ILogger<RedisEventBus>>(),
            Mock.Of<ILogger<RabbitMQEventBus>>(),
            Mock.Of<ILogger<KafkaEventBus>>());

        var gateway = new FinancialPlatform.PaymentService.Services.PaymentGateway(
            Mock.Of<ILogger<FinancialPlatform.PaymentService.Services.PaymentGateway>>());

        var service = new FinancialPlatform.PaymentService.Services.PaymentService(
            bus, gateway, Mock.Of<ILogger<FinancialPlatform.PaymentService.Services.PaymentService>>());

        service.RegisterTransaction("tx_cleanup", 100m);
        service.RecordPinVerification("tx_cleanup", false);

        PaymentAuthorizedEvent? first = null;
        await bus.SubscribeAsync<PaymentAuthorizedEvent>(e => { first = e; return Task.CompletedTask; });
        await service.ProcessPaymentAsync(new RiskEvaluatedEvent("tx_cleanup", 10, "LOW", [], DateTime.UtcNow));
        Assert.NotNull(first);
        Assert.False(first.Authorized);

        service.RegisterTransaction("tx_cleanup", 100m);
        PaymentAuthorizedEvent? second = null;
        await bus.SubscribeAsync<PaymentAuthorizedEvent>(e => { second = e; return Task.CompletedTask; });
        await service.ProcessPaymentAsync(new RiskEvaluatedEvent("tx_cleanup", 10, "LOW", [], DateTime.UtcNow));
        Assert.NotNull(second);
        Assert.True(second.Authorized);
    }
}
