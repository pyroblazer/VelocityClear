using FinancialPlatform.ComplianceService.Data;
using FinancialPlatform.Shared.DTOs;
using FinancialPlatform.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace FinancialPlatform.ComplianceService.Services;

public class AccessControlService
{
    private readonly ComplianceDbContext _db;
    private readonly ILogger<AccessControlService> _logger;

    // Roles that conflict with each other (SoD constraints)
    private static readonly HashSet<(string, string)> _conflictingRoles = new()
    {
        ("Maker", "Checker"),
        ("Trader", "RiskOfficer"),
        ("Admin", "Auditor")
    };

    public AccessControlService(ComplianceDbContext db, ILogger<AccessControlService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<RoleAssignment> AssignRoleAsync(AssignRoleRequest request)
    {
        var currentRoles = await _db.RoleAssignments
            .Where(r => r.UserId == request.UserId && r.IsActive)
            .Select(r => r.Role)
            .ToListAsync();

        foreach (var existing in currentRoles)
        {
            if (_conflictingRoles.Contains((existing, request.Role)) ||
                _conflictingRoles.Contains((request.Role, existing)))
                throw new InvalidOperationException(
                    $"Role '{request.Role}' conflicts with existing role '{existing}' (SoD violation)");
        }

        var assignment = new RoleAssignment
        {
            UserId = request.UserId,
            Role = request.Role,
            AssignedBy = request.AssignedBy,
            ApprovalRequestId = request.ApprovalRequestId
        };
        _db.RoleAssignments.Add(assignment);
        await _db.SaveChangesAsync();

        _logger.LogInformation("Role {Role} assigned to {UserId} by {AssignedBy}",
            request.Role, request.UserId, request.AssignedBy);
        return assignment;
    }

    public async Task<AccessCheckResponse> CheckAccessAsync(AccessCheckRequest request)
    {
        var roles = await _db.RoleAssignments
            .Where(r => r.UserId == request.UserId && r.IsActive)
            .Select(r => r.Role)
            .ToListAsync();

        var allowed = EvaluateAbac(roles, request.Resource, request.Action);
        return new AccessCheckResponse(allowed, request.UserId, request.Resource, request.Action,
            allowed ? null : "Insufficient permissions");
    }

    public async Task<IEnumerable<RoleAssignment>> GetUserRolesAsync(string userId)
    {
        return await _db.RoleAssignments
            .Where(r => r.UserId == userId && r.IsActive)
            .ToListAsync();
    }

    private static bool EvaluateAbac(List<string> roles, string resource, string action)
    {
        if (roles.Contains("Admin")) return true;
        return (resource, action) switch
        {
            ("audit", "read") => roles.Any(r => r is "Auditor" or "Admin" or "ComplianceOfficer"),
            ("audit", "write") => roles.Any(r => r is "Admin"),
            ("transaction", "read") => roles.Any(r => r is "User" or "Admin" or "Auditor"),
            ("transaction", "write") => roles.Any(r => r is "User" or "Admin"),
            ("compliance", _) => roles.Any(r => r is "ComplianceOfficer" or "Admin"),
            ("sar", _) => roles.Any(r => r is "ComplianceOfficer" or "AmlOfficer" or "Admin"),
            _ => roles.Contains("Admin")
        };
    }
}
