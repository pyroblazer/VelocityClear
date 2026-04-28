using FinancialPlatform.ComplianceService.Data;
using FinancialPlatform.ComplianceService.Services;
using FinancialPlatform.EventInfrastructure.Bus;
using FinancialPlatform.EventInfrastructure.Sse;
using FinancialPlatform.PaymentService.Services;
using FinancialPlatform.RiskService.Services;
using FinancialPlatform.Shared.Events;
using FinancialPlatform.Shared.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace FinancialPlatform.IntegrationTests;

public class EventFlowTests
{
    private async Task<(AdaptiveEventBus Bus, ComplianceDbContext Db)> CreateInfrastructure()
    {
        var bus = new AdaptiveEventBus(
            Mock.Of<ILogger<AdaptiveEventBus>>(),
            Mock.Of<ILogger<InMemoryEventBus>>(),
            Mock.Of<ILogger<RedisEventBus>>(),
            Mock.Of<ILogger<RabbitMQEventBus>>(),
            Mock.Of<ILogger<KafkaEventBus>>());

        var options = new DbContextOptionsBuilder<ComplianceDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        var db = new ComplianceDbContext(options);

        return (bus, db);
    }

    [Fact]
    public async Task FullEventFlow_TransactionCreated_To_AuditLogged()
    {
        var (bus, db) = await CreateInfrastructure();

        // Setup risk service subscriber
        var riskService = new RiskEvaluationService(bus, new AmlRuleEngine(Mock.Of<ILogger<AmlRuleEngine>>()), Mock.Of<ILogger<RiskEvaluationService>>());
        await bus.SubscribeAsync<TransactionCreatedEvent>(e => riskService.EvaluateAsync(e));

        // Setup payment service subscriber
        var paymentGateway = new PaymentGateway(Mock.Of<ILogger<PaymentGateway>>());
        var paymentService = new FinancialPlatform.PaymentService.Services.PaymentService(
            bus, paymentGateway, Mock.Of<ILogger<FinancialPlatform.PaymentService.Services.PaymentService>>());
        await bus.SubscribeAsync<RiskEvaluatedEvent>(e => paymentService.ProcessPaymentAsync(e));

        // Setup audit service subscriber
        var auditService = new AuditService(db, Mock.Of<ISseHub>(), Mock.Of<ILogger<AuditService>>());
        await bus.SubscribeAsync<TransactionCreatedEvent>(e => auditService.LogEventAsync("TransactionCreated", e));
        await bus.SubscribeAsync<RiskEvaluatedEvent>(e => auditService.LogEventAsync("RiskEvaluated", e));
        await bus.SubscribeAsync<PaymentAuthorizedEvent>(e => auditService.LogEventAsync("PaymentAuthorized", e));

        // Publish transaction
        paymentService.RegisterTransaction("tx_flow_001", 250m);
        await bus.PublishAsync(new TransactionCreatedEvent("tx_flow_001", "user_flow", 250m, "USD",
            new DateTime(2026, 4, 19, 14, 0, 0, DateTimeKind.Utc)));

        // Allow async handlers to complete
        await Task.Delay(200);

        // Verify audit trail
        var logs = await db.AuditLogs.OrderBy(l => l.CreatedAt).ToListAsync();
        Assert.True(logs.Count >= 2, $"Expected at least 2 audit logs, got {logs.Count}");
        Assert.Contains(logs, l => l.EventType == "TransactionCreated");
        Assert.Contains(logs, l => l.EventType == "RiskEvaluated");

        // Verify hash chain integrity
        Assert.Null(logs[0].PreviousHash);
        for (int i = 1; i < logs.Count; i++)
        {
            Assert.Equal(logs[i - 1].Hash, logs[i].PreviousHash);
        }
    }

    [Fact]
    public async Task HighRiskTransaction_GeneratesRejectedPaymentEvent()
    {
        var (bus, db) = await CreateInfrastructure();

        var riskService = new RiskEvaluationService(bus, new AmlRuleEngine(Mock.Of<ILogger<AmlRuleEngine>>()), Mock.Of<ILogger<RiskEvaluationService>>());
        await bus.SubscribeAsync<TransactionCreatedEvent>(e => riskService.EvaluateAsync(e));

        PaymentAuthorizedEvent? paymentEvent = null;
        await bus.SubscribeAsync<PaymentAuthorizedEvent>(e => { paymentEvent = e; return Task.CompletedTask; });

        var paymentGateway = new PaymentGateway(Mock.Of<ILogger<PaymentGateway>>());
        var paymentService = new FinancialPlatform.PaymentService.Services.PaymentService(
            bus, paymentGateway, Mock.Of<ILogger<FinancialPlatform.PaymentService.Services.PaymentService>>());
        await bus.SubscribeAsync<RiskEvaluatedEvent>(e => paymentService.ProcessPaymentAsync(e));

        // Large amount at odd hour -> high risk -> rejected
        paymentService.RegisterTransaction("tx_high_risk", 20000m);
        await bus.PublishAsync(new TransactionCreatedEvent("tx_high_risk", "user_risk", 20000m, "USD",
            new DateTime(2026, 4, 19, 3, 0, 0, DateTimeKind.Utc)));

        await Task.Delay(200);

        Assert.NotNull(paymentEvent);
        Assert.False(paymentEvent.Authorized);
    }

    [Fact]
    public async Task LowRiskTransaction_GeneratesAuthorizedPaymentEvent()
    {
        var (bus, _) = await CreateInfrastructure();

        var riskService = new RiskEvaluationService(bus, new AmlRuleEngine(Mock.Of<ILogger<AmlRuleEngine>>()), Mock.Of<ILogger<RiskEvaluationService>>());
        await bus.SubscribeAsync<TransactionCreatedEvent>(e => riskService.EvaluateAsync(e));

        PaymentAuthorizedEvent? paymentEvent = null;
        await bus.SubscribeAsync<PaymentAuthorizedEvent>(e => { paymentEvent = e; return Task.CompletedTask; });

        var paymentGateway = new PaymentGateway(Mock.Of<ILogger<PaymentGateway>>());
        var paymentService = new FinancialPlatform.PaymentService.Services.PaymentService(
            bus, paymentGateway, Mock.Of<ILogger<FinancialPlatform.PaymentService.Services.PaymentService>>());
        await bus.SubscribeAsync<RiskEvaluatedEvent>(e => paymentService.ProcessPaymentAsync(e));

        // Small amount during day -> low risk -> authorized
        paymentService.RegisterTransaction("tx_low_risk", 50m);
        await bus.PublishAsync(new TransactionCreatedEvent("tx_low_risk", "user_safe", 50m, "EUR",
            new DateTime(2026, 4, 19, 10, 0, 0, DateTimeKind.Utc)));

        await Task.Delay(200);

        Assert.NotNull(paymentEvent);
        Assert.True(paymentEvent.Authorized);
    }
}
