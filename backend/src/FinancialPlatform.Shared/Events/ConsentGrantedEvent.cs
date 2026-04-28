using FinancialPlatform.Shared.Enums;

namespace FinancialPlatform.Shared.Events;

public record ConsentGrantedEvent(
    string ConsentId,
    string UserId,
    ConsentType ConsentType,
    string? IpAddress,
    DateTime Timestamp
);
