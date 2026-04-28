using FinancialPlatform.Shared.Enums;

namespace FinancialPlatform.Shared.Models;

public class KycProfile
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string UserId { get; set; } = string.Empty;
    public KycStatus Status { get; set; } = KycStatus.Pending;
    public string? FullName { get; set; }
    public string? IdNumber { get; set; }
    public string? IdType { get; set; }
    public DateTime? IdExpiryDate { get; set; }
    public bool LivenessChecked { get; set; }
    public double LivenessConfidence { get; set; }
    public bool WatchlistScreened { get; set; }
    public bool WatchlistHit { get; set; }
    public string? WatchlistMatchedCategory { get; set; }
    public string? RejectionReason { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? VerifiedAt { get; set; }
    public DateTime? ExpiresAt { get; set; }
}
