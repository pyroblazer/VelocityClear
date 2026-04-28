using FinancialPlatform.Shared.Enums;

namespace FinancialPlatform.Shared.Models;

public class ApprovalRequest
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public ApprovalType ApprovalType { get; set; }
    public ApprovalStatus Status { get; set; } = ApprovalStatus.PendingApproval;
    public string RequestedBy { get; set; } = string.Empty;
    public string? ApprovedBy { get; set; }
    public string RequestedData { get; set; } = string.Empty;
    public string? Comments { get; set; }
    public string? RejectionReason { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAt { get; set; } = DateTime.UtcNow.AddHours(24);
    public DateTime? ProcessedAt { get; set; }
    public string? ResourceId { get; set; }
    public string? ResourceType { get; set; }
}
