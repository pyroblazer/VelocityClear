using FinancialPlatform.ComplianceService.Services;
using FinancialPlatform.Shared.DTOs;
using FinancialPlatform.Shared.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FinancialPlatform.ComplianceService.Controllers;

[ApiController]
[Route("api/aml")]
public class AmlController : ControllerBase
{
    private readonly AmlMonitoringService _aml;
    private readonly SarService _sar;

    public AmlController(AmlMonitoringService aml, SarService sar)
    {
        _aml = aml;
        _sar = sar;
    }

    [HttpGet("alerts")]
    public async Task<IActionResult> ListAlerts([FromQuery] AlertStatus? status)
    {
        var alerts = await _aml.ListAlertsAsync(status);
        return Ok(alerts);
    }

    [HttpGet("alerts/{alertId}")]
    public async Task<IActionResult> GetAlert(string alertId)
    {
        var alert = await _aml.GetAlertAsync(alertId);
        return alert == null ? NotFound() : Ok(alert);
    }

    [HttpPost("alerts/{alertId}/resolve")]
    [Authorize(Roles = "Admin,ComplianceOfficer,AmlOfficer")]
    public async Task<IActionResult> ResolveAlert(string alertId, [FromBody] ResolveAlertRequest request)
    {
        var result = await _aml.ResolveAlertAsync(alertId, request);
        return result == null ? NotFound() : Ok(result);
    }

    [HttpPost("sar")]
    [Authorize(Roles = "Admin,ComplianceOfficer,AmlOfficer")]
    public async Task<IActionResult> FileSar([FromBody] SarFilingRequest request)
    {
        var result = await _sar.FileSarAsync(request);
        return Ok(result);
    }

    [HttpGet("sar")]
    public async Task<IActionResult> ListSars([FromQuery] SarStatus? status)
    {
        var sars = await _sar.ListSarsAsync(status);
        return Ok(sars);
    }

    [HttpGet("sar/{sarId}")]
    public async Task<IActionResult> GetSar(string sarId)
    {
        var sar = await _sar.GetSarAsync(sarId);
        return sar == null ? NotFound() : Ok(sar);
    }

    [HttpPut("sar/{sarId}/status")]
    [Authorize(Roles = "Admin,ComplianceOfficer")]
    public async Task<IActionResult> UpdateSarStatus(string sarId,
        [FromQuery] SarStatus newStatus, [FromBody] string? notes)
    {
        var result = await _sar.UpdateSarStatusAsync(sarId, newStatus, notes);
        return result == null ? NotFound() : Ok(result);
    }
}
