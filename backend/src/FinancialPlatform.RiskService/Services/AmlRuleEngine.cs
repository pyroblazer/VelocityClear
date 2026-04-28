using FinancialPlatform.Shared.Enums;

namespace FinancialPlatform.RiskService.Services;

public class AmlRuleEngine
{
    private readonly ILogger<AmlRuleEngine> _logger;

    // Transaction history per user: userId -> list of (timestamp, amount, currency)
    private readonly Dictionary<string, List<(DateTime Timestamp, decimal Amount, string Currency)>> _history = new();
    private readonly object _lock = new();

    public AmlRuleEngine(ILogger<AmlRuleEngine> logger)
    {
        _logger = logger;
    }

    public record AmlRuleResult(string Rule, AlertSeverity Severity, string Description);

    public IReadOnlyList<AmlRuleResult> Evaluate(
        string transactionId, string userId, decimal amount, string currency,
        DateTime timestamp, bool isCrossBorder = false, bool isDormantAccount = false)
    {
        lock (_lock)
        {
            if (!_history.TryGetValue(userId, out var txHistory))
            {
                txHistory = new List<(DateTime, decimal, string)>();
                _history[userId] = txHistory;
            }
            txHistory.Add((timestamp, amount, currency));

            // Prune history older than 7 days
            txHistory.RemoveAll(t => t.Timestamp < DateTime.UtcNow.AddDays(-7));
        }

        var results = new List<AmlRuleResult>();

        if (DetectStructuring(userId, amount)) results.Add(new AmlRuleResult(
            "STRUCTURING", AlertSeverity.High,
            "Multiple transactions near reporting threshold detected (structuring)"));

        if (DetectVelocity1h(userId, timestamp)) results.Add(new AmlRuleResult(
            "VELOCITY_1H", AlertSeverity.Medium,
            "High transaction velocity in the last hour"));

        if (DetectVelocity24h(userId, timestamp)) results.Add(new AmlRuleResult(
            "VELOCITY_24H", AlertSeverity.High,
            "High transaction velocity in the last 24 hours"));

        if (DetectVelocity7d(userId, timestamp)) results.Add(new AmlRuleResult(
            "VELOCITY_7D", AlertSeverity.Medium,
            "Unusual transaction velocity over 7 days"));

        if (DetectRoundAmount(amount)) results.Add(new AmlRuleResult(
            "ROUND_AMOUNT", AlertSeverity.Low,
            "Suspiciously round transaction amount"));

        if (isCrossBorder && amount > 10000m) results.Add(new AmlRuleResult(
            "CROSS_BORDER", AlertSeverity.Medium,
            "Large cross-border transaction requires reporting"));

        if (isDormantAccount && amount > 5000m) results.Add(new AmlRuleResult(
            "DORMANT_ACTIVATION", AlertSeverity.High,
            "Dormant account reactivated with large transaction"));

        if (results.Count > 0)
            _logger.LogWarning("AML rules triggered for {UserId} tx {TxId}: {Rules}",
                userId, transactionId, string.Join(", ", results.Select(r => r.Rule)));

        return results;
    }

    private bool DetectStructuring(string userId, decimal amount)
    {
        // Transactions between 8000 and 9999 (just under 10000 reporting threshold)
        if (amount < 8000m || amount >= 10000m) return false;
        lock (_lock)
        {
            if (!_history.TryGetValue(userId, out var h)) return false;
            var recent = h.Count(t => t.Timestamp >= DateTime.UtcNow.AddDays(-3)
                && t.Amount >= 8000m && t.Amount < 10000m);
            return recent >= 3;
        }
    }

    private bool DetectVelocity1h(string userId, DateTime timestamp)
    {
        lock (_lock)
        {
            if (!_history.TryGetValue(userId, out var h)) return false;
            return h.Count(t => t.Timestamp >= timestamp.AddHours(-1)) > 10;
        }
    }

    private bool DetectVelocity24h(string userId, DateTime timestamp)
    {
        lock (_lock)
        {
            if (!_history.TryGetValue(userId, out var h)) return false;
            return h.Count(t => t.Timestamp >= timestamp.AddHours(-24)) > 50;
        }
    }

    private bool DetectVelocity7d(string userId, DateTime timestamp)
    {
        lock (_lock)
        {
            if (!_history.TryGetValue(userId, out var h)) return false;
            return h.Count(t => t.Timestamp >= timestamp.AddDays(-7)) > 200;
        }
    }

    private static bool DetectRoundAmount(decimal amount) =>
        amount >= 1000m && amount % 1000m == 0;
}
