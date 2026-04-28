using FinancialPlatform.RiskService.Services;
using FinancialPlatform.Shared.Enums;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace FinancialPlatform.UnitTests.Services;

public class AmlRuleEngineTests
{
    private static AmlRuleEngine CreateEngine() =>
        new AmlRuleEngine(Mock.Of<ILogger<AmlRuleEngine>>());

    [Fact]
    public void SmallNormalTransaction_NoRules()
    {
        var engine = CreateEngine();
        var results = engine.Evaluate("tx1", "user1", 100m, "IDR", DateTime.UtcNow);
        Assert.Empty(results);
    }

    [Fact]
    public void RoundAmount_TriggersRoundAmountRule()
    {
        var engine = CreateEngine();
        var results = engine.Evaluate("tx1", "user1", 5000m, "IDR", DateTime.UtcNow);
        Assert.Contains(results, r => r.Rule == "ROUND_AMOUNT");
    }

    [Fact]
    public void CrossBorderLargeAmount_TriggersCrossBorderRule()
    {
        var engine = CreateEngine();
        var results = engine.Evaluate("tx1", "user1", 15000m, "USD", DateTime.UtcNow, isCrossBorder: true);
        Assert.Contains(results, r => r.Rule == "CROSS_BORDER");
    }

    [Fact]
    public void CrossBorderSmallAmount_NoRule()
    {
        var engine = CreateEngine();
        var results = engine.Evaluate("tx1", "user1", 500m, "USD", DateTime.UtcNow, isCrossBorder: true);
        Assert.DoesNotContain(results, r => r.Rule == "CROSS_BORDER");
    }

    [Fact]
    public void DormantWithLargeAmount_TriggersDormantRule()
    {
        var engine = CreateEngine();
        var results = engine.Evaluate("tx1", "user1", 10000m, "IDR", DateTime.UtcNow, isDormantAccount: true);
        Assert.Contains(results, r => r.Rule == "DORMANT_ACTIVATION");
    }

    [Fact]
    public void VelocityExceeds1HourThreshold_TriggersVelocity1h()
    {
        var engine = CreateEngine();
        var now = DateTime.UtcNow;
        for (int i = 0; i < 11; i++)
            engine.Evaluate($"tx{i}", "fastuser", 100m, "IDR", now);

        var results = engine.Evaluate("tx_final", "fastuser", 100m, "IDR", now);
        Assert.Contains(results, r => r.Rule == "VELOCITY_1H");
    }

    [Fact]
    public void RoundAmountRule_SeverityIsLow()
    {
        var engine = CreateEngine();
        var results = engine.Evaluate("tx1", "user1", 2000m, "IDR", DateTime.UtcNow);
        var rule = results.FirstOrDefault(r => r.Rule == "ROUND_AMOUNT");
        Assert.NotNull(rule);
        Assert.Equal(AlertSeverity.Low, rule.Severity);
    }
}
