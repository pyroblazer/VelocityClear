namespace FinancialPlatform.Shared.Models;

public class ComplaintNote
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string ComplaintId { get; set; } = string.Empty;
    public string AuthorId { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public bool IsInternal { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
