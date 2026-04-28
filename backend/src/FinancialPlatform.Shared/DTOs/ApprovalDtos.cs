using FinancialPlatform.Shared.Enums;

namespace FinancialPlatform.Shared.DTOs;

public record CreateApprovalRequest(
    ApprovalType ApprovalType,
    string RequestedBy,
    string RequestedData,
    string? ResourceId,
    string? ResourceType,
    string? Comments
);

public record ProcessApprovalRequest(
    string ProcessedBy,
    bool Approved,
    string? Comments,
    string? RejectionReason
);

public record ApprovalResponse(
    string Id,
    ApprovalType ApprovalType,
    ApprovalStatus Status,
    string RequestedBy,
    string? ApprovedBy,
    string? Comments,
    DateTime CreatedAt,
    DateTime ExpiresAt,
    DateTime? ProcessedAt,
    string? ResourceId,
    string? ResourceType
);

public record AssignRoleRequest(
    string UserId,
    string Role,
    string AssignedBy,
    string? ApprovalRequestId
);

public record AccessCheckRequest(string UserId, string Resource, string Action);

public record AccessCheckResponse(bool Allowed, string UserId, string Resource, string Action, string? Reason);
