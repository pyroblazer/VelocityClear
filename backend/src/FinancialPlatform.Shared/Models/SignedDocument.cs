using FinancialPlatform.Shared.Enums;

namespace FinancialPlatform.Shared.Models;

public class SignedDocument
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string DocumentId { get; set; } = string.Empty;
    public string SignerId { get; set; } = string.Empty;
    public DocumentSigningStatus Status { get; set; } = DocumentSigningStatus.Pending;
    public string? Signature { get; set; }
    public string? DocumentHash { get; set; }
    public string? VendorReferenceId { get; set; }
    public string SigningMethod { get; set; } = "HMAC-SHA256";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? SignedAt { get; set; }
    public DateTime? VerifiedAt { get; set; }
    public DateTime? ExpiresAt { get; set; }
}
