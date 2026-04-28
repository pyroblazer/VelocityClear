using FinancialPlatform.Shared.Enums;

namespace FinancialPlatform.Shared.Events;

public record ApprovalRequestedEvent(
    string ApprovalId,
    ApprovalType ApprovalType,
    string RequestedBy,
    string? ResourceId,
    DateTime Timestamp
);
