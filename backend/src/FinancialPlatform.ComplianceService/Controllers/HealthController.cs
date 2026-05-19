using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FinancialPlatform.ComplianceService.Controllers;

[ApiController]
[Route("api/compliance/health")]
public class HealthController : ControllerBase
{
    [AllowAnonymous]
    [HttpGet]
    public IActionResult Health()
    {
        return Ok(new
        {
            service = "ComplianceService",
            status = "Healthy",
            timestamp = DateTime.UtcNow
        });
    }
}
