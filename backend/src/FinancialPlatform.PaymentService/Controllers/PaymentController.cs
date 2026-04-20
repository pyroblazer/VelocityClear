// ============================================================================
// PaymentController.cs - HTTP API Controller for Payment Operations
// ============================================================================
// This controller exposes endpoints for checking payment status and manually
// triggering payment authorization. In normal operation, payments are authorized
// automatically via events; these endpoints support testing and monitoring.
//
// Key concepts:
//   - Tuple deconstruction: "var (authorized, reason) = _gateway.Authorize(...)"
//     unpacks a tuple return value into separate named variables.
//   - Anonymous types: "new { property = value }" creates a type with no name,
//     useful for one-off JSON responses without defining a class.
//   - record type: A concise way to define immutable data transfer objects.
// ============================================================================

using FinancialPlatform.PaymentService.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FinancialPlatform.PaymentService.Controllers;

[Authorize]
[ApiController]
// Fixed route "api/payment" - not using [controller] substitution.
[Route("api/payment")]
public class PaymentController : ControllerBase
{
    private readonly PaymentGateway _gateway;
    private readonly ILogger<PaymentController> _logger;

    // Constructor injection of PaymentGateway and logger.
    public PaymentController(PaymentGateway gateway, ILogger<PaymentController> logger)
    {
        _gateway = gateway;
        _logger = logger;
    }

    // Health check endpoint - useful for load balancers and monitoring systems.
    [AllowAnonymous]
    [HttpGet("health")]
    public IActionResult Health()
    {
        // Anonymous type: "new { ... }" creates an object with typed properties
        // but no explicit class definition. ASP.NET Core serializes it to JSON.
        return Ok(new
        {
            service = "PaymentService",
            status = "Healthy",
            timestamp = DateTime.UtcNow
        });
    }

    // Get the status of a transaction's payment processing.
    [HttpGet("status/{transactionId}")]
    public IActionResult GetStatus(string transactionId)
    {
        _logger.LogDebug("Status check for tx {TxId}", transactionId);

        // In a production system this would look up the payment status from a database.
        // For this implementation, we return a placeholder indicating the service is tracking it.
        return Ok(new
        {
            transactionId,
            status = "Tracked",
            message = "Payment status is tracked via event flow. Final status is published as PaymentAuthorizedEvent.",
            timestamp = DateTime.UtcNow
        });
    }

    // Manually trigger payment authorization (primarily for testing).
    [HttpPost("authorize")]
    public IActionResult Authorize([FromBody] AuthorizeRequest request)
    {
        if (request.Amount <= 0)
        {
            return BadRequest(new { error = "Amount must be greater than zero" });
        }

        // Tuple deconstruction: Authorize() returns a tuple (bool, string).
        // The "var (authorized, reason)" syntax unpacks the tuple elements into
        // two separate variables in a single statement. This is equivalent to:
        //   var result = _gateway.Authorize(request.Amount, request.RiskScore);
        //   bool authorized = result.Authorized;
        //   string reason = result.Reason;
        var (authorized, reason) = _gateway.Authorize(request.Amount, request.RiskScore);

        return Ok(new
        {
            authorized,
            reason,
            amount = request.Amount,
            riskScore = request.RiskScore,
            timestamp = DateTime.UtcNow
        });
    }
}

// A "record" with a primary constructor - the parameters automatically become
// public init-only properties. Records are ideal for request/response DTOs
// because they're immutable and have built-in equality comparison.
public record AuthorizeRequest(decimal Amount, int RiskScore);
