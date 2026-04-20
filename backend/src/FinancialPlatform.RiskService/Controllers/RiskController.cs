// ============================================================================
// RiskController.cs - HTTP API Controller for Risk Evaluation
// ============================================================================
// This controller exposes endpoints for the Risk Service. It provides a health
// check endpoint and a manual risk evaluation endpoint. In production, risk
// evaluations are triggered automatically by events; this manual endpoint is
// useful for testing and ad-hoc evaluation.
//
// Key concepts:
//   - [FromBody]: Deserializes the JSON request body into a C# object.
//   - Accepted(): Returns HTTP 202 (Accepted), meaning the request was received
//     but processing happens asynchronously - the client doesn't wait for a result.
//   - record type: A lightweight, immutable reference type ideal for DTOs.
//     Records provide value-based equality, concise syntax, and with-expressions.
//   - Nullable reference types (string?): The "?" suffix means the property
//     can be null without causing a compiler warning.
// ============================================================================

using FinancialPlatform.Shared.Events;
using FinancialPlatform.RiskService.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FinancialPlatform.RiskService.Controllers;

[Authorize]
[ApiController]
// Explicit route "api/risk" - unlike [controller] token substitution, this is
// a fixed route that won't change if the class is renamed.
[Route("api/risk")]
public class RiskController : ControllerBase
{
    private readonly RiskEvaluationService _riskService;
    private readonly ILogger<RiskController> _logger;

    // Constructor injection: the DI container provides RiskEvaluationService
    // and ILogger automatically.
    public RiskController(RiskEvaluationService riskService, ILogger<RiskController> logger)
    {
        _riskService = riskService;
        _logger = logger;
    }

    // [HttpGet("health")] maps to GET /api/risk/health
    [AllowAnonymous]
    [HttpGet("health")]
    // IActionResult is a flexible return type that can represent any HTTP response.
    public IActionResult Health()
    {
        // Ok() returns HTTP 200 with a JSON body. The "new { ... }" syntax
        // creates an anonymous type - its properties become JSON keys.
        return Ok(new
        {
            service = "RiskService",
            status = "Healthy",
            timestamp = DateTime.UtcNow
        });
    }

    // [HttpPost("evaluate")] maps to POST /api/risk/evaluate
    [HttpPost("evaluate")]
    public async Task<IActionResult> Evaluate([FromBody] EvaluateRequest request)
    {
        // Input validation: ensure required string fields are not null or empty.
        // "||" is the logical OR operator (short-circuit: evaluates the right
        // side only if the left side is false).
        if (string.IsNullOrEmpty(request.TransactionId) || string.IsNullOrEmpty(request.UserId))
        {
            return BadRequest(new { error = "TransactionId and UserId are required" });
        }

        // Structured logging: {TxId} is a named placeholder that becomes a
        // property in the log entry, enabling filtering in log aggregation tools.
        _logger.LogInformation("Manual risk evaluation requested for tx {TxId}", request.TransactionId);

        // Construct a TransactionCreatedEvent from the request to feed into
        // the risk evaluation pipeline. This simulates the event that would
        // normally arrive from the event bus.
        var evt = new TransactionCreatedEvent(
            request.TransactionId,
            request.UserId,
            request.Amount,
            // The "??" (null-coalescing operator) returns the left side if it's
            // not null, otherwise the right side. Here it provides a default value.
            request.Currency ?? "USD",
            request.Timestamp ?? DateTime.UtcNow
        );

        await _riskService.EvaluateAsync(evt);

        // Accepted() returns HTTP 202 (Accepted), indicating the request was
        // accepted for processing but the result is not yet available. This is
        // appropriate for asynchronous operations where the client doesn't need
        // to wait for completion.
        return Accepted(new
        {
            message = "Risk evaluation initiated",
            transactionId = request.TransactionId
        });
    }
}

// A "record" is a special C# type (introduced in C# 9) for immutable data objects.
// The "primary constructor" syntax (parameters in parentheses after the type name)
// automatically generates:
//   - Public init-only properties for each parameter
//   - A constructor that accepts all parameters
//   - Value-based Equals(), GetHashCode(), and ToString()
//
// "string?" means the property is nullable - it can be assigned null without
// a compiler warning. C# 8+ has nullable reference types enabled by default
// in new projects, so you must explicitly mark reference types as nullable.
public record EvaluateRequest(
    string TransactionId,
    string UserId,
    decimal Amount,
    string? Currency = null,    // Default value: null if not provided
    DateTime? Timestamp = null  // Default value: null if not provided
);
