namespace FinancialPlatform.Shared.Models;

public class VendorAuditEntry
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string VendorName { get; set; } = string.Empty;
    public string ServiceType { get; set; } = string.Empty;
    public double UptimePercent { get; set; }
    public double SlaTargetPercent { get; set; }
    public bool SlaMet { get; set; }
    public int IncidentCount { get; set; }
    public DateTime PeriodStart { get; set; }
    public DateTime PeriodEnd { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
