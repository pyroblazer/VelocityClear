// ============================================================================
// PaymentGateway.cs - Payment Authorization Rules Engine
// ============================================================================
// This class encapsulates the business rules for authorizing or rejecting
// payments. It evaluates the transaction amount and risk score against
// predefined thresholds to make an authorization decision.
//
// Key concepts:
//   - Tuple return type: "(bool Authorized, string Reason)" returns two named
//     values from a single method. The caller can deconstruct them with
//     "var (authorized, reason) = gateway.Authorize(...)".
//   - decimal type: A 128-bit floating-point type designed for financial
//     calculations. Unlike float/double, it represents decimal fractions
//     exactly (e.g., 0.1 is truly 0.1, not 0.10000000000000001).
//   - "m" suffix: Marks a numeric literal as decimal (e.g., 50000m).
// ============================================================================

namespace FinancialPlatform.PaymentService.Services;

public class PaymentGateway
{
    private readonly ILogger<PaymentGateway> _logger;

    public PaymentGateway(ILogger<PaymentGateway> logger)
    {
        _logger = logger;
    }

    // The return type is a tuple with named elements: (bool Authorized, string Reason).
    // Tuples in C# are value types that can group multiple values without defining
    // a class or struct. Named tuple elements make the code more readable.
    public (bool Authorized, string Reason) Authorize(decimal amount, int riskScore)
    {
        _logger.LogDebug("Authorizing payment: Amount={Amount}, RiskScore={RiskScore}", amount, riskScore);

        // Rule 1: Reject transactions above the daily limit.
        // The "m" suffix on 50000m marks this as a decimal literal.
        // decimal is preferred for money because it avoids binary floating-point
        // rounding errors (0.1 + 0.2 == 0.3 in decimal, unlike float/double).
        if (amount > 50000m)
        {
            _logger.LogWarning("Payment rejected: Amount {Amount} exceeds daily limit", amount);
            return (false, "Amount exceeds daily limit");
        }

        // Rule 2: Reject if the risk score is very high (>= 80).
        if (riskScore >= 80)
        {
            _logger.LogWarning("Payment rejected: Risk score {RiskScore} too high", riskScore);
            return (false, "Risk score too high");
        }

        // Rule 3: Reject if the amount is moderately high AND risk is elevated.
        // This catches combinations like $8,000 with a risk score of 60.
        if (amount > 5000m && riskScore >= 50)
        {
            _logger.LogWarning(
                "Payment rejected: High amount ({Amount}) with elevated risk ({RiskScore})",
                amount, riskScore);
            return (false, "High amount with elevated risk");
        }

        // If none of the rejection rules apply, the payment is authorized.
        _logger.LogInformation("Payment authorized: Amount={Amount}, RiskScore={RiskScore}", amount, riskScore);
        return (true, "Authorized");
    }
}
