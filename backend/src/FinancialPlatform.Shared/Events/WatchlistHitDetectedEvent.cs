using FinancialPlatform.Shared.Enums;

namespace FinancialPlatform.Shared.Events;

public record WatchlistHitDetectedEvent(
    string KycProfileId,
    string UserId,
    string MatchedName,
    WatchlistCategory Category,
    double MatchConfidence,
    DateTime Timestamp
);
