// ============================================================================
// PaymentAuthorizedEvent.cs - Payment Authorized Domain Event
//
// This file defines the event published after the PaymentService processes a
// transaction. It is the THIRD event in the pipeline. It indicates whether the
// payment was authorized or denied, along with a reason. The ComplianceService
// listens for this event to create an audit log entry.
//
// Event flow: TransactionCreated -> RiskEvaluated -> PaymentAuthorized -> AuditLogged
// ============================================================================

namespace FinancialPlatform.Shared.Events;

// "bool" is a boolean type in C# (true/false), equivalent to boolean in other
// languages. "Authorized" indicates whether the payment was approved or denied.
// "Reason" explains why (e.g., "Approved", "Insufficient funds", "Risk too high").
public record PaymentAuthorizedEvent(
    string TransactionId,
    bool Authorized,
    string Reason,
    DateTime AuthorizedAt
);
