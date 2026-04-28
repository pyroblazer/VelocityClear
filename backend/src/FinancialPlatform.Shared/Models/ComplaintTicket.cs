using FinancialPlatform.Shared.Enums;

namespace FinancialPlatform.Shared.Models;

public class ComplaintTicket
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string UserId { get; set; } = string.Empty;
    public ComplaintCategory Category { get; set; }
    public ComplaintStatus Status { get; set; } = ComplaintStatus.Submitted;
    public EscalationLevel EscalationLevel { get; set; } = EscalationLevel.Level1;
    public string Subject { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string? AssignedTo { get; set; }
    public string? Resolution { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? AcknowledgedAt { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public DateTime? ClosedAt { get; set; }
    // POJK 20-business-day SLA deadline
    public DateTime SlaDeadline { get; set; } = DateTime.UtcNow.AddDays(28);
    public bool SlaBreach { get; set; }
    public string? RelatedTransactionId { get; set; }
}
