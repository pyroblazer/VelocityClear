using FinancialPlatform.Shared.Enums;

namespace FinancialPlatform.Shared.DTOs;

public record GrantConsentRequest(
    string UserId,
    ConsentType ConsentType,
    string? IpAddress,
    string? UserAgent,
    string? LegalBasis
);

public record WithdrawConsentRequest(
    string UserId,
    ConsentType ConsentType,
    string? Reason
);

public record ConsentResponse(
    string Id,
    string UserId,
    ConsentType ConsentType,
    ConsentStatus Status,
    DateTime GrantedAt,
    DateTime? WithdrawnAt,
    DateTime? ExpiresAt,
    string? LegalBasis
);

public record ConsentCheckResponse(bool HasActiveConsent, ConsentType ConsentType, string UserId);
