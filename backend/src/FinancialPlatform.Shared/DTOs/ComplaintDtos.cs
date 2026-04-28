using FinancialPlatform.Shared.Enums;

namespace FinancialPlatform.Shared.DTOs;

public record CreateComplaintRequest(
    string UserId,
    ComplaintCategory Category,
    string Subject,
    string Description,
    string? RelatedTransactionId
);

public record AddComplaintNoteRequest(
    string AuthorId,
    string Content,
    bool IsInternal
);

public record EscalateComplaintRequest(EscalationLevel NewLevel, string Reason);

public record ResolveComplaintRequest(string Resolution, string ResolvedBy);

public record ComplaintResponse(
    string Id,
    string UserId,
    ComplaintCategory Category,
    ComplaintStatus Status,
    EscalationLevel EscalationLevel,
    string Subject,
    string Description,
    string? AssignedTo,
    string? Resolution,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    DateTime SlaDeadline,
    bool SlaBreach,
    string? RelatedTransactionId
);

public record SignDocumentRequest(
    string DocumentId,
    string SignerId,
    string DocumentContent
);

public record VerifySignatureRequest(
    string DocumentId,
    string Signature,
    string DocumentContent
);

public record SignatureResponse(
    string Id,
    string DocumentId,
    string SignerId,
    DocumentSigningStatus Status,
    string? Signature,
    DateTime CreatedAt,
    DateTime? SignedAt
);
