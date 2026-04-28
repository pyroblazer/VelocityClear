using FinancialPlatform.Shared.Enums;

namespace FinancialPlatform.Shared.DTOs;

public record CreateIncidentRequest(
    string Title,
    string Description,
    IncidentSeverity Severity,
    string? AffectedSystems,
    string? RunbookReference
);

public record UpdateIncidentRequest(
    IncidentStatus NewStatus,
    string? ContainmentActions,
    string? RootCause,
    string? AssignedTo
);

public record IncidentResponse(
    string Id,
    string Title,
    string Description,
    IncidentSeverity Severity,
    IncidentStatus Status,
    string? AssignedTo,
    string? RunbookReference,
    string? AffectedSystems,
    DateTime DetectedAt,
    DateTime? ResolvedAt,
    DateTime CreatedAt
);

public record SocDashboardResponse(
    int OpenIncidents,
    int CriticalIncidents,
    int HighIncidents,
    int ResolvedLast24h,
    IEnumerable<IncidentResponse> RecentIncidents
);

public record DrpStatusResponse(
    string Id,
    string PlanName,
    DrpStatus Status,
    int RtoMinutes,
    int RpoMinutes,
    DateTime? LastTestedAt,
    DateTime? NextTestScheduled,
    bool LastTestPassed
);

public record DataResidencyResponse(
    string ServiceName,
    string Region,
    bool IsCompliant,
    string? NonComplianceReason,
    DateTime CheckedAt
);

public record VendorAuditResponse(
    string VendorName,
    string ServiceType,
    double UptimePercent,
    double SlaTargetPercent,
    bool SlaMet,
    int IncidentCount,
    DateTime PeriodStart,
    DateTime PeriodEnd
);
