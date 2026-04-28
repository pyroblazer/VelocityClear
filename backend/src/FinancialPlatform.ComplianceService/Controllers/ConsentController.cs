using FinancialPlatform.ComplianceService.Services;
using FinancialPlatform.Shared.DTOs;
using FinancialPlatform.Shared.Enums;
using Microsoft.AspNetCore.Mvc;

namespace FinancialPlatform.ComplianceService.Controllers;

[ApiController]
[Route("api/consent")]
public class ConsentController : ControllerBase
{
    private readonly ConsentService _consent;

    public ConsentController(ConsentService consent) => _consent = consent;

    [HttpPost("grant")]
    public async Task<IActionResult> Grant([FromBody] GrantConsentRequest request)
    {
        var result = await _consent.GrantConsentAsync(request);
        return Ok(result);
    }

    [HttpPost("withdraw")]
    public async Task<IActionResult> Withdraw([FromBody] WithdrawConsentRequest request)
    {
        try
        {
            var result = await _consent.WithdrawConsentAsync(request);
            return Ok(result);
        }
        catch (KeyNotFoundException ex) { return NotFound(new { error = ex.Message }); }
    }

    [HttpGet("user/{userId}")]
    public async Task<IActionResult> ListByUser(string userId)
    {
        var records = await _consent.ListConsentsByUserAsync(userId);
        return Ok(records);
    }

    [HttpGet("user/{userId}/check/{consentType}")]
    public async Task<IActionResult> Check(string userId, ConsentType consentType)
    {
        var result = await _consent.CheckConsentAsync(userId, consentType);
        return Ok(result);
    }
}
