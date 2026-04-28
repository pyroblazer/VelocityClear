using FinancialPlatform.ComplianceService.Services;
using FinancialPlatform.Shared.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FinancialPlatform.ComplianceService.Controllers;

[ApiController]
[Route("api/access-control")]
[Authorize(Roles = "Admin")]
public class AccessControlController : ControllerBase
{
    private readonly AccessControlService _access;

    public AccessControlController(AccessControlService access) => _access = access;

    [HttpPost("assign-role")]
    public async Task<IActionResult> AssignRole([FromBody] AssignRoleRequest request)
    {
        try
        {
            var result = await _access.AssignRoleAsync(request);
            return Ok(result);
        }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpGet("user/{userId}/roles")]
    public async Task<IActionResult> GetRoles(string userId)
    {
        var roles = await _access.GetUserRolesAsync(userId);
        return Ok(roles);
    }

    [HttpPost("check")]
    public async Task<IActionResult> CheckAccess([FromBody] AccessCheckRequest request)
    {
        var result = await _access.CheckAccessAsync(request);
        return Ok(result);
    }
}
