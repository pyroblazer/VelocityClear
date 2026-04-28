using FinancialPlatform.Shared.Enums;

namespace FinancialPlatform.Shared.Models;

public class ConsentRecord
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string UserId { get; set; } = string.Empty;
    public ConsentType ConsentType { get; set; }
    public ConsentStatus Status { get; set; } = ConsentStatus.Granted;
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public DateTime GrantedAt { get; set; } = DateTime.UtcNow;
    public DateTime? WithdrawnAt { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public string? LegalBasis { get; set; }
    public int Version { get; set; } = 1;
}
