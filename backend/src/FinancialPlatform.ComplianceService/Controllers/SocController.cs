using FinancialPlatform.ComplianceService.Services;
using FinancialPlatform.Shared.DTOs;
using FinancialPlatform.Shared.Enums;
using Microsoft.AspNetCore.Mvc;

namespace FinancialPlatform.ComplianceService.Controllers;

[ApiController]
[Route("api/soc")]
public class SocController : ControllerBase
{
    private readonly SocService _soc;

    public SocController(SocService soc) => _soc = soc;

    [HttpPost("incidents")]
    public async Task<IActionResult> CreateIncident([FromBody] CreateIncidentRequest request)
    {
        var result = await _soc.CreateIncidentAsync(request);
        return Ok(result);
    }

    [HttpGet("incidents")]
    public async Task<IActionResult> ListIncidents([FromQuery] IncidentStatus? status)
    {
        var results = await _soc.ListIncidentsAsync(status);
        return Ok(results);
    }

    [HttpPut("incidents/{incidentId}")]
    public async Task<IActionResult> UpdateIncident(string incidentId, [FromBody] UpdateIncidentRequest request)
    {
        var result = await _soc.UpdateIncidentAsync(incidentId, request);
        return result == null ? NotFound() : Ok(result);
    }

    [HttpGet("dashboard")]
    public async Task<IActionResult> GetDashboard()
    {
        var result = await _soc.GetDashboardAsync();
        return Ok(result);
    }
}
