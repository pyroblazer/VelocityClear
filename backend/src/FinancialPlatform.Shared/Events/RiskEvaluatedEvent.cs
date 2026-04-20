// ============================================================================
// RiskEvaluatedEvent.cs - Risk Evaluated Domain Event
//
// This file defines the event published after the RiskService evaluates a
// transaction. It is the SECOND event in the pipeline. It carries the risk score,
// level, and any flags that were triggered during evaluation. The PaymentService
// listens for this event to decide whether to authorize the payment.
//
// Event flow: TransactionCreated -> RiskEvaluated -> PaymentAuthorized -> AuditLogged
// ============================================================================

namespace FinancialPlatform.Shared.Events;

// "int" is used for RiskScore - a numeric score (e.g., 0-100) representing the
// assessed risk level. "List<string>" holds the risk flags (e.g., "HIGH_VELOCITY",
// "UNUSUAL_AMOUNT") which are a dynamic-length collection of indicators.
public record RiskEvaluatedEvent(
    string TransactionId,
    int RiskScore,
    string RiskLevel,
    List<string> Flags,
    DateTime EvaluatedAt
);
