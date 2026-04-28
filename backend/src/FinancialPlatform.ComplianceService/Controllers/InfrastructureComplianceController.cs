using FinancialPlatform.ComplianceService.Services;
using Microsoft.AspNetCore.Mvc;

namespace FinancialPlatform.ComplianceService.Controllers;

[ApiController]
[Route("api/infrastructure-compliance")]
public class InfrastructureComplianceController : ControllerBase
{
    private readonly InfrastructureComplianceService _infra;

    public InfrastructureComplianceController(InfrastructureComplianceService infra) => _infra = infra;

    [HttpGet("drp")]
    public async Task<IActionResult> GetDrpStatus()
    {
        var result = await _infra.GetDrpStatusAsync();
        return Ok(result);
    }

    [HttpPost("drp")]
    public async Task<IActionResult> UpsertDrpPlan(
        [FromQuery] string planName,
        [FromQuery] int rtoMinutes,
        [FromQuery] int rpoMinutes)
    {
        var result = await _infra.UpsertDrpPlanAsync(planName, rtoMinutes, rpoMinutes);
        return Ok(result);
    }

    [HttpGet("data-residency")]
    public async Task<IActionResult> CheckResidency()
    {
        var result = await _infra.CheckDataResidencyAsync();
        return Ok(result);
    }

    [HttpPost("data-residency")]
    public async Task<IActionResult> RecordResidency(
        [FromQuery] string service,
        [FromQuery] string region,
        [FromQuery] bool compliant,
        [FromQuery] string? reason)
    {
        var result = await _infra.RecordResidencyCheckAsync(service, region, compliant, reason);
        return Ok(result);
    }

    [HttpGet("vendors")]
    public async Task<IActionResult> GetVendors()
    {
        var result = await _infra.GetVendorAuditsAsync();
        return Ok(result);
    }

    [HttpPost("vendors")]
    public async Task<IActionResult> RecordVendorAudit(
        [FromQuery] string vendor,
        [FromQuery] string serviceType,
        [FromQuery] double uptime,
        [FromQuery] double slaTarget,
        [FromQuery] int incidents,
        [FromQuery] DateTime periodStart,
        [FromQuery] DateTime periodEnd)
    {
        var result = await _infra.RecordVendorAuditAsync(vendor, serviceType, uptime, slaTarget, incidents, periodStart, periodEnd);
        return Ok(result);
    }
}
