using FinancialPlatform.Shared.Enums;

namespace FinancialPlatform.Shared.DTOs;

public record AlertResponse(
    string Id,
    string TransactionId,
    string UserId,
    string RuleTriggered,
    AlertSeverity Severity,
    AlertStatus Status,
    string? AssignedTo,
    decimal TransactionAmount,
    string Currency,
    DateTime CreatedAt,
    DateTime UpdatedAt
);

public record ResolveAlertRequest(string Resolution, string ResolvedBy, AlertStatus NewStatus);

public record SarFilingRequest(
    string TransactionId,
    string UserId,
    string Narrative,
    decimal SuspiciousAmount,
    string SuspiciousBasis,
    string FiledBy
);

public record SarResponse(
    string Id,
    string TransactionId,
    string UserId,
    string Narrative,
    SarStatus Status,
    string? OjkReferenceNumber,
    string? FiledBy,
    DateTime? FiledAt,
    decimal SuspiciousAmount,
    DateTime CreatedAt
);
