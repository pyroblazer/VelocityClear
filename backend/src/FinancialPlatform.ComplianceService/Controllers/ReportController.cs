using FinancialPlatform.ComplianceService.Services;
using FinancialPlatform.Shared.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FinancialPlatform.ComplianceService.Controllers;

[ApiController]
[Route("api/reports")]
public class ReportController : ControllerBase
{
    private readonly ReportingService _reporting;

    public ReportController(ReportingService reporting) => _reporting = reporting;

    [HttpPost]
    [Authorize(Roles = "Admin,ComplianceOfficer,Auditor")]
    public async Task<IActionResult> Generate([FromBody] GenerateReportRequest request)
    {
        var result = await _reporting.GenerateReportAsync(request);
        return Ok(result);
    }

    [HttpGet]
    public async Task<IActionResult> List()
    {
        var results = await _reporting.ListReportsAsync();
        return Ok(results);
    }

    [HttpGet("{reportId}")]
    public async Task<IActionResult> Get(string reportId)
    {
        var report = await _reporting.GetReportAsync(reportId);
        return report == null ? NotFound() : Ok(report);
    }

    [HttpGet("{reportId}/download")]
    public async Task<IActionResult> Download(string reportId)
    {
        var report = await _reporting.GetReportAsync(reportId);
        if (report == null) return NotFound();
        if (report.Content == null) return BadRequest(new { error = "Report content not yet available" });

        var (contentType, ext) = report.Format switch
        {
            Shared.Enums.ReportFormat.Xml => ("application/xml", "xml"),
            Shared.Enums.ReportFormat.Csv => ("text/csv", "csv"),
            _ => ("application/json", "json")
        };

        var filename = $"ojk-report-{report.ReportType}-{report.PeriodStart:yyyyMM}.{ext}";
        return File(System.Text.Encoding.UTF8.GetBytes(report.Content), contentType, filename);
    }
}
