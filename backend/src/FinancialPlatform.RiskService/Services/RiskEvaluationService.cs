// ============================================================================
// RiskEvaluationService.cs - Risk Scoring Engine
// ============================================================================
// This service evaluates the risk level of financial transactions based on
// multiple factors: transaction amount, velocity (frequency of transactions
// per user), and time of day. It maintains in-memory state to track transaction
// velocity per user and publishes risk evaluation events.
//
// Key concepts:
//   - Dictionary<string, List<DateTime>>: A nested generic collection. The
//     outer Dictionary maps user IDs to Lists of timestamps. Generic collections
//     (<T>) provide type safety - you can only add the correct type of elements.
//   - lock statement: Provides mutual exclusion for thread safety. Only one
//     thread can execute code inside a lock block at a time, preventing race
//     conditions when multiple requests modify shared state simultaneously.
//   - Pattern matching (switch expression): A concise way to map values -
//     "score switch { >= 80 => "HIGH", ... }" checks the score against patterns.
//   - "is" pattern: Used for range checks like "evt.Timestamp.Hour is < 6 or > 22".
// ============================================================================

using FinancialPlatform.RiskService;
using FinancialPlatform.Shared.Events;
using FinancialPlatform.Shared.Interfaces;

namespace FinancialPlatform.RiskService.Services;

public class RiskEvaluationService
{
    private readonly IEventBus _eventBus;
    private readonly AmlRuleEngine _amlRuleEngine;
    private readonly ILogger<RiskEvaluationService> _logger;

    // Dictionary<string, List<DateTime>> is a nested generic collection:
    //   - The outer Dictionary maps string keys (user IDs) to List<DateTime> values.
    //   - Each user's value is a List of DateTime objects representing recent
    //     transaction timestamps.
    // This is used to calculate "velocity" - how many transactions a user has
    // made recently.
    private readonly Dictionary<string, List<DateTime>> _velocityTracker = new();

    // This object serves as a lock root for thread synchronization. The "lock"
    // statement requires a reference type as its key - this dedicated object
    // ensures no external code can lock on the same object.
    private readonly object _velocityLock = new();

    public RiskEvaluationService(IEventBus eventBus, AmlRuleEngine amlRuleEngine, ILogger<RiskEvaluationService> logger)
    {
        _eventBus = eventBus;
        _amlRuleEngine = amlRuleEngine;
        _logger = logger;
    }

    public async Task EvaluateAsync(TransactionCreatedEvent evt)
    {
        // Record this transaction's timestamp in the velocity tracker.
        RecordTransaction(evt.UserId, evt.Timestamp);

        // Calculate a numeric risk score (0-100) based on multiple factors.
        var score = CalculateRiskScore(evt);

        // Switch expression with pattern matching - a concise way to classify
        // the score into risk levels. Patterns are evaluated top to bottom:
        //   - >= 80 matches scores of 80 or higher -> "HIGH"
        //   - >= 50 matches scores of 50-79 -> "MEDIUM"
        //   - _ is the discard pattern (catch-all) -> "LOW"
        var level = score switch
        {
            >= 80 => "HIGH",
            >= 50 => "MEDIUM",
            _ => "LOW"
        };

        ServiceMetrics.RiskScore.WithLabels(level).Set(score);
        ServiceMetrics.RiskEvaluationsTotal.WithLabels(level).Inc();

        // Build a list of risk flags - indicators of suspicious activity.
        var flags = new List<string>();

        // The "m" suffix on 10000m marks it as a decimal literal.
        // decimal is a 128-bit numeric type ideal for financial calculations
        // because it avoids the rounding errors of float/double.
        if (evt.Amount > 10000m) flags.Add("HIGH_AMOUNT");

        if (DetectVelocity(evt.UserId)) flags.Add("HIGH_VELOCITY");

        // "is < 6 or > 22" uses C# pattern matching with the "or" combinator.
        // It checks if the hour is before 6 AM or after 10 PM (odd hours).
        if (evt.Timestamp.Hour is < 6 or > 22) flags.Add("ODD_HOUR");

        // AML rules
        var amlResults = _amlRuleEngine.Evaluate(evt.TransactionId, evt.UserId, evt.Amount,
            evt.Currency, evt.Timestamp);
        foreach (var r in amlResults)
            flags.Add(r.Rule);

        // Structured logging with multiple placeholders. string.Join() concatenates
        // the flags with ", " as a separator.
        _logger.LogInformation(
            "Risk evaluated for tx {TxId}: Score={Score}, Level={Level}, Flags=[{Flags}]",
            evt.TransactionId, score, level, string.Join(", ", flags));

        // Publish a RiskEvaluatedEvent to the event bus so downstream services
        // (like PaymentService) can react to the risk assessment.
        await _eventBus.PublishAsync(new RiskEvaluatedEvent(
            evt.TransactionId, score, level, flags, DateTime.UtcNow));
    }

    private int CalculateRiskScore(TransactionCreatedEvent evt)
    {
        int score = 0;

        // Amount-based risk: larger amounts contribute more to the score.
        if (evt.Amount > 5000m) score += 30;
        if (evt.Amount > 10000m) score += 20;

        // Velocity-based risk: frequent transactions from the same user.
        if (DetectVelocity(evt.UserId)) score += 25;

        // Anomaly: transactions during unusual hours (between 10 PM and 6 AM).
        if (evt.Timestamp.Hour is < 6 or > 22) score += 15;

        // Math.Min() caps the score at 100 (the maximum risk score).
        return Math.Min(score, 100);
    }

    private void RecordTransaction(string userId, DateTime timestamp)
    {
        // The "lock" statement provides mutual exclusion. When one thread enters
        // the lock block, all other threads trying to enter a lock on the same
        // object (_velocityLock) must wait. This prevents race conditions where
        // two threads could read/modify the dictionary simultaneously.
        lock (_velocityLock)
        {
            // TryGetValue() is an efficient pattern for dictionary access:
            //   - If the key exists, it returns true and sets "timestamps" to the value.
            //   - If the key doesn't exist, it returns false and sets "timestamps" to default.
            // The "out var" syntax declares the variable inline - you don't need
            // to declare it separately before calling the method.
            if (!_velocityTracker.TryGetValue(userId, out var timestamps))
            {
                timestamps = new List<DateTime>();
                _velocityTracker[userId] = timestamps;
            }

            timestamps.Add(timestamp);

            // Prune entries older than 1 minute to keep memory bounded.
            // This ensures the dictionary doesn't grow indefinitely.
            var cutoff = DateTime.UtcNow.AddMinutes(-1);

            // RemoveAll() removes all elements matching the predicate (lambda).
            // "t => t < cutoff" means "remove timestamps older than the cutoff."
            timestamps.RemoveAll(t => t < cutoff);
        }
    }

    private bool DetectVelocity(string userId)
    {
        // Another lock block to safely read from the shared dictionary.
        // Both RecordTransaction and DetectVelocity lock on the same object
        // (_velocityLock), ensuring they can't modify/read simultaneously.
        lock (_velocityLock)
        {
            if (!_velocityTracker.TryGetValue(userId, out var timestamps))
                return false;

            var cutoff = DateTime.UtcNow.AddMinutes(-1);

            // .Count() with a predicate counts elements matching the condition.
            // If more than 5 transactions in the last minute, that's high velocity.
            var recentCount = timestamps.Count(t => t >= cutoff);
            return recentCount > 5;
        }
    }
}
