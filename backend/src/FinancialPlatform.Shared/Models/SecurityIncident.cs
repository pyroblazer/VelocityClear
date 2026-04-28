using FinancialPlatform.Shared.Enums;

namespace FinancialPlatform.Shared.Models;

public class SecurityIncident
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public IncidentSeverity Severity { get; set; }
    public IncidentStatus Status { get; set; } = IncidentStatus.Detected;
    public string? AssignedTo { get; set; }
    public string? RunbookReference { get; set; }
    public string? AffectedSystems { get; set; }
    public string? ContainmentActions { get; set; }
    public string? RootCause { get; set; }
    public DateTime DetectedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ContainedAt { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public DateTime? ClosedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
