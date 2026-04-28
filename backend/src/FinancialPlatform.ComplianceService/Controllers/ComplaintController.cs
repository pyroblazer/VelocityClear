using FinancialPlatform.ComplianceService.Services;
using FinancialPlatform.Shared.DTOs;
using Microsoft.AspNetCore.Mvc;

namespace FinancialPlatform.ComplianceService.Controllers;

[ApiController]
[Route("api/complaints")]
public class ComplaintController : ControllerBase
{
    private readonly ComplaintService _complaints;

    public ComplaintController(ComplaintService complaints) => _complaints = complaints;

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateComplaintRequest request)
    {
        var result = await _complaints.CreateComplaintAsync(request);
        return CreatedAtAction(nameof(Get), new { complaintId = result.Id }, result);
    }

    [HttpGet("{complaintId}")]
    public async Task<IActionResult> Get(string complaintId)
    {
        var result = await _complaints.GetComplaintAsync(complaintId);
        return result == null ? NotFound() : Ok(result);
    }

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] string? userId)
    {
        var results = await _complaints.ListComplaintsAsync(userId);
        return Ok(results);
    }

    [HttpPost("{complaintId}/acknowledge")]
    public async Task<IActionResult> Acknowledge(string complaintId)
    {
        var result = await _complaints.AcknowledgeAsync(complaintId);
        return result == null ? NotFound() : Ok(result);
    }

    [HttpPost("{complaintId}/escalate")]
    public async Task<IActionResult> Escalate(string complaintId, [FromBody] EscalateComplaintRequest request)
    {
        var result = await _complaints.EscalateAsync(complaintId, request);
        return result == null ? NotFound() : Ok(result);
    }

    [HttpPost("{complaintId}/resolve")]
    public async Task<IActionResult> Resolve(string complaintId, [FromBody] ResolveComplaintRequest request)
    {
        var result = await _complaints.ResolveAsync(complaintId, request);
        return result == null ? NotFound() : Ok(result);
    }

    [HttpPost("{complaintId}/close")]
    public async Task<IActionResult> Close(string complaintId)
    {
        var result = await _complaints.CloseAsync(complaintId);
        return result == null ? NotFound() : Ok(result);
    }

    [HttpPost("{complaintId}/notes")]
    public async Task<IActionResult> AddNote(string complaintId, [FromBody] AddComplaintNoteRequest request)
    {
        await _complaints.AddNoteAsync(complaintId, request);
        return Ok();
    }

    [HttpGet("{complaintId}/sla")]
    public async Task<IActionResult> CheckSla(string complaintId)
    {
        var result = await _complaints.GetComplaintAsync(complaintId);
        if (result == null) return NotFound();
        return Ok(new { complaintId, slaDeadline = result.SlaDeadline, slaBreach = result.SlaBreach });
    }
}
