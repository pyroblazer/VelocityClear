using FinancialPlatform.Shared.Enums;

namespace FinancialPlatform.Shared.Events;

public record ConsentWithdrawnEvent(
    string ConsentId,
    string UserId,
    ConsentType ConsentType,
    string? Reason,
    DateTime Timestamp
);
