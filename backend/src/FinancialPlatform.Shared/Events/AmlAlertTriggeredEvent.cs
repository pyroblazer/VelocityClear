using FinancialPlatform.Shared.Enums;

namespace FinancialPlatform.Shared.Events;

public record AmlAlertTriggeredEvent(
    string AlertId,
    string TransactionId,
    string UserId,
    string RuleTriggered,
    AlertSeverity Severity,
    decimal Amount,
    string Currency,
    DateTime Timestamp
);
