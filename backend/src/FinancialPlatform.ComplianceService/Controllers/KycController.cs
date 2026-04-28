using FinancialPlatform.ComplianceService.Services;
using FinancialPlatform.Shared.DTOs;
using FinancialPlatform.Shared.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FinancialPlatform.ComplianceService.Controllers;

[ApiController]
[Route("api/kyc")]
public class KycController : ControllerBase
{
    private readonly KycService _kyc;

    public KycController(KycService kyc) => _kyc = kyc;

    [HttpPost("initiate")]
    public async Task<IActionResult> Initiate([FromBody] InitiateKycRequest request)
    {
        var result = await _kyc.InitiateKycAsync(request);
        return Ok(result);
    }

    [HttpPost("{kycProfileId}/liveness")]
    public async Task<IActionResult> PerformLiveness(string kycProfileId, [FromBody] string userId)
    {
        try
        {
            var result = await _kyc.PerformLivenessCheckAsync(new LivenessCheckRequest(kycProfileId, userId));
            return Ok(result);
        }
        catch (KeyNotFoundException ex) { return NotFound(new { error = ex.Message }); }
    }

    [HttpPost("{kycProfileId}/screen")]
    public async Task<IActionResult> Screen(string kycProfileId, [FromBody] WatchlistScreenRequest request)
    {
        try
        {
            var result = await _kyc.ScreenWatchlistAsync(request with { KycProfileId = kycProfileId });
            return Ok(result);
        }
        catch (KeyNotFoundException ex) { return NotFound(new { error = ex.Message }); }
    }

    [HttpGet("{kycProfileId}")]
    public async Task<IActionResult> GetProfile(string kycProfileId)
    {
        var profile = await _kyc.GetProfileAsync(kycProfileId);
        return profile == null ? NotFound() : Ok(profile);
    }

    [HttpGet("user/{userId}")]
    public async Task<IActionResult> GetByUser(string userId)
    {
        var profile = await _kyc.GetProfileByUserAsync(userId);
        return profile == null ? NotFound() : Ok(profile);
    }

    [HttpGet("user/{userId}/verified")]
    public async Task<IActionResult> IsVerified(string userId)
    {
        var verified = await _kyc.IsUserVerifiedAsync(userId);
        return Ok(new { userId, isVerified = verified });
    }

    [HttpPut("{kycProfileId}/status")]
    [Authorize(Roles = "Admin,ComplianceOfficer")]
    public async Task<IActionResult> UpdateStatus(string kycProfileId, [FromBody] UpdateKycStatusRequest request)
    {
        try
        {
            var result = await _kyc.UpdateStatusAsync(kycProfileId, request);
            return Ok(result);
        }
        catch (KeyNotFoundException ex) { return NotFound(new { error = ex.Message }); }
    }
}
