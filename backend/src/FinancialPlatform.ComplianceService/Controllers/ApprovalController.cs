using FinancialPlatform.ComplianceService.Services;
using FinancialPlatform.Shared.DTOs;
using FinancialPlatform.Shared.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FinancialPlatform.ComplianceService.Controllers;

[ApiController]
[Route("api/approvals")]
public class ApprovalController : ControllerBase
{
    private readonly ApprovalService _approval;

    public ApprovalController(ApprovalService approval) => _approval = approval;

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateApprovalRequest request)
    {
        var result = await _approval.CreateApprovalAsync(request);
        return CreatedAtAction(nameof(GetApproval), new { approvalId = result.Id }, result);
    }

    [HttpGet("{approvalId}")]
    public async Task<IActionResult> GetApproval(string approvalId)
    {
        var result = await _approval.GetApprovalAsync(approvalId);
        return result == null ? NotFound() : Ok(result);
    }

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] ApprovalStatus? status)
    {
        var results = await _approval.ListApprovalsAsync(status);
        return Ok(results);
    }

    [HttpPost("{approvalId}/process")]
    [Authorize(Roles = "Admin,Checker")]
    public async Task<IActionResult> Process(string approvalId, [FromBody] ProcessApprovalRequest request)
    {
        try
        {
            var result = await _approval.ProcessApprovalAsync(approvalId, request);
            return Ok(result);
        }
        catch (KeyNotFoundException ex) { return NotFound(new { error = ex.Message }); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }
}
