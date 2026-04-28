using FinancialPlatform.Shared.Enums;

namespace FinancialPlatform.Shared.Models;

public class DrpBcpStatus
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string PlanName { get; set; } = string.Empty;
    public DrpStatus Status { get; set; } = DrpStatus.Active;
    public int RtoMinutes { get; set; }
    public int RpoMinutes { get; set; }
    public DateTime? LastTestedAt { get; set; }
    public DateTime? NextTestScheduled { get; set; }
    public bool LastTestPassed { get; set; }
    public string? TestNotes { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
