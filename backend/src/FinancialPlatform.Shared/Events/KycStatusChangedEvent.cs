using FinancialPlatform.Shared.Enums;

namespace FinancialPlatform.Shared.Events;

public record KycStatusChangedEvent(
    string KycProfileId,
    string UserId,
    KycStatus OldStatus,
    KycStatus NewStatus,
    string? Reason,
    DateTime Timestamp
);
