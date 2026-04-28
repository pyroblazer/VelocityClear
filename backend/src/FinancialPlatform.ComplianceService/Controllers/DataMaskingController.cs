using FinancialPlatform.ComplianceService.Services;
using FinancialPlatform.Shared.DTOs;
using Microsoft.AspNetCore.Mvc;

namespace FinancialPlatform.ComplianceService.Controllers;

[ApiController]
[Route("api/data-masking")]
public class DataMaskingController : ControllerBase
{
    private readonly DataMaskingService _masking;

    public DataMaskingController(DataMaskingService masking) => _masking = masking;

    [HttpPost("mask")]
    public IActionResult Mask([FromBody] MaskDataRequest request)
    {
        var result = _masking.MaskValue(request);
        return Ok(result);
    }

    [HttpGet("classifications")]
    public async Task<IActionResult> ListClassifications()
    {
        var result = await _masking.ListClassificationsAsync();
        return Ok(result);
    }
}
