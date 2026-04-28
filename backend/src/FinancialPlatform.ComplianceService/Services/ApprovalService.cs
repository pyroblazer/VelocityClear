using FinancialPlatform.ComplianceService.Data;
using FinancialPlatform.Shared.DTOs;
using FinancialPlatform.Shared.Enums;
using FinancialPlatform.Shared.Events;
using FinancialPlatform.Shared.Interfaces;
using FinancialPlatform.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace FinancialPlatform.ComplianceService.Services;

public class ApprovalService
{
    private readonly ComplianceDbContext _db;
    private readonly IEventBus _eventBus;
    private readonly ILogger<ApprovalService> _logger;

    private static readonly HashSet<ApprovalType> _requiresApproval = new()
    {
        ApprovalType.UserCreation,
        ApprovalType.RoleChange,
        ApprovalType.SarFiling,
        ApprovalType.ReportGeneration
    };

    public ApprovalService(ComplianceDbContext db, IEventBus eventBus, ILogger<ApprovalService> logger)
    {
        _db = db;
        _eventBus = eventBus;
        _logger = logger;
    }

    public async Task<ApprovalResponse> CreateApprovalAsync(CreateApprovalRequest request)
    {
        var approval = new ApprovalRequest
        {
            ApprovalType = request.ApprovalType,
            RequestedBy = request.RequestedBy,
            RequestedData = request.RequestedData,
            ResourceId = request.ResourceId,
            ResourceType = request.ResourceType,
            Comments = request.Comments
        };
        _db.ApprovalRequests.Add(approval);
        await _db.SaveChangesAsync();

        await _eventBus.PublishAsync(new ApprovalRequestedEvent(
            approval.Id, approval.ApprovalType, approval.RequestedBy,
            approval.ResourceId, DateTime.UtcNow));

        _logger.LogInformation("Approval requested: {ApprovalId} type={Type} by={By}",
            approval.Id, approval.ApprovalType, approval.RequestedBy);
        return MapToResponse(approval);
    }

    public async Task<ApprovalResponse> ProcessApprovalAsync(string approvalId, ProcessApprovalRequest request)
    {
        var approval = await _db.ApprovalRequests.FindAsync(approvalId)
            ?? throw new KeyNotFoundException($"Approval {approvalId} not found");

        if (approval.Status != ApprovalStatus.PendingApproval)
            throw new InvalidOperationException($"Approval is already {approval.Status}");

        // Maker-checker: approver cannot be the same as requester
        if (approval.RequestedBy == request.ProcessedBy)
            throw new InvalidOperationException("Approver cannot be the same person as the requester (maker-checker violation)");

        approval.Status = request.Approved ? ApprovalStatus.Approved : ApprovalStatus.Rejected;
        approval.ApprovedBy = request.ProcessedBy;
        approval.Comments = request.Comments;
        approval.RejectionReason = request.RejectionReason;
        approval.ProcessedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        await _eventBus.PublishAsync(new ApprovalCompletedEvent(
            approval.Id, approval.ApprovalType, approval.Status,
            request.ProcessedBy, request.Comments, DateTime.UtcNow));

        _logger.LogInformation("Approval processed: {ApprovalId} result={Status} by={By}",
            approvalId, approval.Status, request.ProcessedBy);
        return MapToResponse(approval);
    }

    public async Task<IEnumerable<ApprovalResponse>> ListApprovalsAsync(ApprovalStatus? status = null)
    {
        var query = _db.ApprovalRequests.AsQueryable();
        if (status.HasValue)
            query = query.Where(a => a.Status == status.Value);
        var approvals = await query.OrderByDescending(a => a.CreatedAt).ToListAsync();
        return approvals.Select(MapToResponse);
    }

    public async Task<ApprovalResponse?> GetApprovalAsync(string approvalId)
    {
        var approval = await _db.ApprovalRequests.FindAsync(approvalId);
        return approval == null ? null : MapToResponse(approval);
    }

    public bool RequiresApproval(ApprovalType type) => _requiresApproval.Contains(type);

    private static ApprovalResponse MapToResponse(ApprovalRequest a) => new(
        a.Id, a.ApprovalType, a.Status, a.RequestedBy, a.ApprovedBy,
        a.Comments, a.CreatedAt, a.ExpiresAt, a.ProcessedAt,
        a.ResourceId, a.ResourceType);
}
