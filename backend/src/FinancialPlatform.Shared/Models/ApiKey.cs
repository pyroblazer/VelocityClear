namespace FinancialPlatform.Shared.Models;

public class ApiKey
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string KeyHash { get; set; } = string.Empty;
    public string KeyPrefix { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string Permissions { get; set; } = string.Empty; // JSON array stored as string
    public DateTime CreatedAt { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public DateTime? LastUsedAt { get; set; }
    public bool IsActive { get; set; } = true;
}
