using FinancialPlatform.Shared.Enums;

namespace FinancialPlatform.Shared.Models;

public class WatchlistEntry
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string FullName { get; set; } = string.Empty;
    public string? Aliases { get; set; }
    public WatchlistCategory Category { get; set; }
    public string? Nationality { get; set; }
    public string? IdNumber { get; set; }
    public string? Source { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime AddedAt { get; set; } = DateTime.UtcNow;
    public DateTime? RemovedAt { get; set; }
}
