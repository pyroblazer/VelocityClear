namespace FinancialPlatform.Shared.Events;

public record SarFiledEvent(
    string SarId,
    string TransactionId,
    string UserId,
    string FiledBy,
    decimal SuspiciousAmount,
    DateTime Timestamp
);
