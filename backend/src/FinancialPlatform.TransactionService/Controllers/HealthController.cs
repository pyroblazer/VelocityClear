using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FinancialPlatform.TransactionService.Controllers;

[ApiController]
[Route("api/transactions/health")]
public class HealthController : ControllerBase
{
    [AllowAnonymous]
    [HttpGet]
    public IActionResult Health()
    {
        return Ok(new
        {
            service = "TransactionService",
            status = "Healthy",
            timestamp = DateTime.UtcNow
        });
    }
}
