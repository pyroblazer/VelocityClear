namespace FinancialPlatform.Shared.Models;

public class AuditRetentionPolicy
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string EventType { get; set; } = string.Empty;
    public int RetentionYears { get; set; } = 5;
    public string Regulation { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
