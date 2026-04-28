namespace FinancialPlatform.Shared.Models;

public class DataResidencyCheck
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string ServiceName { get; set; } = string.Empty;
    public string DataCategory { get; set; } = string.Empty;
    public string Region { get; set; } = string.Empty;
    public bool IsCompliant { get; set; }
    public string? NonComplianceReason { get; set; }
    public string Regulation { get; set; } = "OJK Data Residency";
    public DateTime CheckedAt { get; set; } = DateTime.UtcNow;
}
