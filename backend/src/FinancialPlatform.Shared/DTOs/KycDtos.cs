using FinancialPlatform.Shared.Enums;

namespace FinancialPlatform.Shared.DTOs;

public record InitiateKycRequest(
    string UserId,
    string FullName,
    string IdNumber,
    string IdType,
    DateTime? IdExpiryDate
);

public record KycProfileResponse(
    string Id,
    string UserId,
    KycStatus Status,
    string? FullName,
    string? IdType,
    bool LivenessChecked,
    double LivenessConfidence,
    bool WatchlistScreened,
    bool WatchlistHit,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    DateTime? VerifiedAt,
    DateTime? ExpiresAt
);

public record LivenessCheckRequest(string KycProfileId, string UserId);

public record LivenessCheckResponse(
    string KycProfileId,
    bool Passed,
    double Confidence,
    string Message
);

public record WatchlistScreenRequest(string KycProfileId, string FullName, string? IdNumber);

public record WatchlistScreenResponse(
    string KycProfileId,
    bool HitFound,
    string? MatchedName,
    string? MatchedCategory,
    double MatchConfidence,
    string Message
);

public record UpdateKycStatusRequest(KycStatus NewStatus, string? Reason);
