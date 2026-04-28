using FinancialPlatform.Shared.Enums;

namespace FinancialPlatform.Shared.Models;

public class OjkReport
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public ReportType ReportType { get; set; }
    public ReportFormat Format { get; set; }
    public ReportStatus Status { get; set; } = ReportStatus.Generating;
    public string? Content { get; set; }
    public string GeneratedBy { get; set; } = string.Empty;
    public DateTime PeriodStart { get; set; }
    public DateTime PeriodEnd { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
    public DateTime? SubmittedAt { get; set; }
    public string? SubmittedTo { get; set; }
    public int RetentionYears { get; set; } = 10;
    public string? ErrorMessage { get; set; }
}
