using FinancialPlatform.Shared.Enums;

namespace FinancialPlatform.Shared.DTOs;

public record GenerateReportRequest(
    ReportType ReportType,
    ReportFormat Format,
    DateTime PeriodStart,
    DateTime PeriodEnd,
    string GeneratedBy
);

public record ReportResponse(
    string Id,
    ReportType ReportType,
    ReportFormat Format,
    ReportStatus Status,
    string GeneratedBy,
    DateTime PeriodStart,
    DateTime PeriodEnd,
    DateTime CreatedAt,
    DateTime? CompletedAt,
    DateTime? SubmittedAt,
    int RetentionYears
);
