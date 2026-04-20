// ============================================================================
// AuditLoggedEvent.cs - Audit Logged Domain Event
//
// This file defines the event published after the ComplianceService records an
// audit log entry. It is the FOURTH and FINAL event in the transaction pipeline.
// It confirms that the event has been immutably recorded in the audit trail and
// includes the SHA-256 hash that links this entry into the hash chain.
//
// Event flow: TransactionCreated -> RiskEvaluated -> PaymentAuthorized -> AuditLogged
// ============================================================================

namespace FinancialPlatform.Shared.Events;

// The "Hash" is the SHA-256 hash of the audit log entry, used for tamper detection.
// The "EventType" identifies what kind of original event was logged (e.g.,
// "TransactionCreated", "RiskEvaluated"). The "AuditId" links back to the audit
// log record for traceability.
public record AuditLoggedEvent(
    string AuditId,
    string EventType,
    string Hash,
    DateTime LoggedAt
);
