using FinancialPlatform.ComplianceService.Services;
using FinancialPlatform.Shared.DTOs;
using Microsoft.AspNetCore.Mvc;

namespace FinancialPlatform.ComplianceService.Controllers;

[ApiController]
[Route("api/digital-signature")]
public class DigitalSignatureController : ControllerBase
{
    private readonly DigitalSignatureService _sig;

    public DigitalSignatureController(DigitalSignatureService sig) => _sig = sig;

    [HttpPost("sign")]
    public async Task<IActionResult> Sign([FromBody] SignDocumentRequest request)
    {
        var result = await _sig.SignDocumentAsync(request);
        return Ok(result);
    }

    [HttpPost("verify")]
    public async Task<IActionResult> Verify([FromBody] VerifySignatureRequest request)
    {
        var result = await _sig.VerifySignatureAsync(request);
        return result == null ? NotFound() : Ok(result);
    }

    [HttpGet("{documentId}")]
    public async Task<IActionResult> Get(string documentId)
    {
        var result = await _sig.GetSignatureAsync(documentId);
        return result == null ? NotFound() : Ok(result);
    }
}
