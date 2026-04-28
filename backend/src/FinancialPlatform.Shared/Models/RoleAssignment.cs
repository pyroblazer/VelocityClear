namespace FinancialPlatform.Shared.Models;

public class RoleAssignment
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string UserId { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public string AssignedBy { get; set; } = string.Empty;
    public DateTime AssignedAt { get; set; } = DateTime.UtcNow;
    public DateTime? RevokedAt { get; set; }
    public string? RevokedBy { get; set; }
    public bool IsActive { get; set; } = true;
    public string? ApprovalRequestId { get; set; }
}
