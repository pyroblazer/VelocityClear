using System.Text;
using System.Xml.Linq;
using FinancialPlatform.ComplianceService.Data;
using FinancialPlatform.Shared.DTOs;
using FinancialPlatform.Shared.Enums;
using FinancialPlatform.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace FinancialPlatform.ComplianceService.Services;

public class ReportingService
{
    private readonly ComplianceDbContext _db;
    private readonly WormStorageService _worm;
    private readonly ILogger<ReportingService> _logger;

    public ReportingService(ComplianceDbContext db, WormStorageService worm, ILogger<ReportingService> logger)
    {
        _db = db;
        _worm = worm;
        _logger = logger;
    }

    public async Task<ReportResponse> GenerateReportAsync(GenerateReportRequest request)
    {
        var report = new OjkReport
        {
            ReportType = request.ReportType,
            Format = request.Format,
            Status = ReportStatus.Generating,
            GeneratedBy = request.GeneratedBy,
            PeriodStart = request.PeriodStart,
            PeriodEnd = request.PeriodEnd
        };
        _db.OjkReports.Add(report);
        await _db.SaveChangesAsync();

        try
        {
            var auditLogs = await _db.AuditLogs
                .Where(a => a.CreatedAt >= request.PeriodStart && a.CreatedAt <= request.PeriodEnd)
                .ToListAsync();

            var amlAlerts = await _db.AmlAlerts
                .Where(a => a.CreatedAt >= request.PeriodStart && a.CreatedAt <= request.PeriodEnd)
                .ToListAsync();

            report.Content = request.Format switch
            {
                ReportFormat.Xml => BuildXmlReport(request, auditLogs, amlAlerts),
                ReportFormat.Csv => BuildCsvReport(request, auditLogs, amlAlerts),
                _ => BuildJsonReport(request, auditLogs, amlAlerts)
            };

            report.Status = ReportStatus.Completed;
            report.CompletedAt = DateTime.UtcNow;

            _worm.Store(report.Id, report.Content);
        }
        catch (Exception ex)
        {
            report.Status = ReportStatus.Failed;
            report.ErrorMessage = ex.Message;
            _logger.LogError(ex, "Report generation failed for {ReportId}", report.Id);
        }

        await _db.SaveChangesAsync();
        _logger.LogInformation("Report {ReportId} generated: status={Status}", report.Id, report.Status);
        return MapToResponse(report);
    }

    public async Task<IEnumerable<ReportResponse>> ListReportsAsync()
    {
        var reports = await _db.OjkReports.OrderByDescending(r => r.CreatedAt).ToListAsync();
        return reports.Select(MapToResponse);
    }

    public async Task<OjkReport?> GetReportAsync(string reportId)
        => await _db.OjkReports.FindAsync(reportId);

    private static string BuildJsonReport(GenerateReportRequest req, List<Shared.Models.AuditLog> logs, List<AmlAlert> alerts)
    {
        return System.Text.Json.JsonSerializer.Serialize(new
        {
            reportType = req.ReportType.ToString(),
            period = new { start = req.PeriodStart, end = req.PeriodEnd },
            generatedAt = DateTime.UtcNow,
            generatedBy = req.GeneratedBy,
            auditLogCount = logs.Count,
            amlAlertCount = alerts.Count,
            highSeverityAlerts = alerts.Count(a => a.Severity >= AlertSeverity.High),
            eventTypeSummary = logs.GroupBy(l => l.EventType)
                .Select(g => new { eventType = g.Key, count = g.Count() })
        });
    }

    private static string BuildXmlReport(GenerateReportRequest req, List<Shared.Models.AuditLog> logs, List<AmlAlert> alerts)
    {
        var doc = new XDocument(
            new XElement("OjkReport",
                new XElement("ReportType", req.ReportType.ToString()),
                new XElement("Period",
                    new XElement("Start", req.PeriodStart.ToString("o")),
                    new XElement("End", req.PeriodEnd.ToString("o"))),
                new XElement("GeneratedAt", DateTime.UtcNow.ToString("o")),
                new XElement("GeneratedBy", req.GeneratedBy),
                new XElement("Summary",
                    new XElement("AuditLogCount", logs.Count),
                    new XElement("AmlAlertCount", alerts.Count),
                    new XElement("HighSeverityAlerts", alerts.Count(a => a.Severity >= AlertSeverity.High))),
                new XElement("EventTypes",
                    logs.GroupBy(l => l.EventType)
                        .Select(g => new XElement("EventType",
                            new XAttribute("name", g.Key),
                            new XAttribute("count", g.Count()))))));
        return doc.ToString();
    }

    private static string BuildCsvReport(GenerateReportRequest req, List<Shared.Models.AuditLog> logs, List<AmlAlert> alerts)
    {
        var sb = new StringBuilder();
        sb.AppendLine("ReportType,PeriodStart,PeriodEnd,GeneratedAt,GeneratedBy,AuditLogCount,AmlAlertCount");
        sb.AppendLine($"{req.ReportType},{req.PeriodStart:o},{req.PeriodEnd:o},{DateTime.UtcNow:o},{req.GeneratedBy},{logs.Count},{alerts.Count}");
        sb.AppendLine();
        sb.AppendLine("EventType,Count");
        foreach (var g in logs.GroupBy(l => l.EventType))
            sb.AppendLine($"{g.Key},{g.Count()}");
        return sb.ToString();
    }

    private static ReportResponse MapToResponse(OjkReport r) => new(
        r.Id, r.ReportType, r.Format, r.Status, r.GeneratedBy,
        r.PeriodStart, r.PeriodEnd, r.CreatedAt, r.CompletedAt,
        r.SubmittedAt, r.RetentionYears);
}
