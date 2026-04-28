using FinancialPlatform.Shared.Enums;

namespace FinancialPlatform.Shared.Events;

public record ApprovalCompletedEvent(
    string ApprovalId,
    ApprovalType ApprovalType,
    ApprovalStatus FinalStatus,
    string ProcessedBy,
    string? Comments,
    DateTime Timestamp
);
