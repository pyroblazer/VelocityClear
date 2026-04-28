using FinancialPlatform.Shared.Enums;

namespace FinancialPlatform.Shared.Models;

public class SuspiciousActivityReport
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string TransactionId { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string Narrative { get; set; } = string.Empty;
    public SarStatus Status { get; set; } = SarStatus.Draft;
    public string? OjkReferenceNumber { get; set; }
    public string? FiledBy { get; set; }
    public DateTime? FiledAt { get; set; }
    public string? ReviewedBy { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public string? ReviewNotes { get; set; }
    public decimal SuspiciousAmount { get; set; }
    public string SuspiciousBasis { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
