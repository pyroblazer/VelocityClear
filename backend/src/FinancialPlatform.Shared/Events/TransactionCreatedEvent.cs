// ============================================================================
// TransactionCreatedEvent.cs - Transaction Created Domain Event
//
// This file defines the event that is published when a new transaction is created.
// It is the FIRST event in the transaction lifecycle pipeline. After publication,
// the RiskService picks it up for risk evaluation. For card transactions (those
// with a PAN and PIN block), the PIN Encryption Service also processes this event.
//
// Event flow (non-card):
//   TransactionCreated -> RiskEvaluated -> PaymentAuthorized -> AuditLogged
//
// Event flow (card transactions):
//   TransactionCreated ──► PinVerified ──┐
//           │                            ├─► PaymentAuthorized ──► AuditLogged
//           └──► RiskEvaluated ──────────┘
//
// Using a "record" here ensures the event is immutable after creation - events
// should never be modified once published, as they represent things that happened.
// ============================================================================

namespace FinancialPlatform.Shared.Events;

// This positional record defines an immutable event object. The parameters become
// init-only properties (they can be set during construction but not afterwards).
// "decimal" is used for Amount to maintain financial precision.
//
// Optional card fields (nullable via "?"):
//   Pan       - Primary Account Number (credit/debit card number, 13-19 digits)
//   PinBlock  - Encrypted PIN block (ISO 9564 Format 0, 16 hex chars)
//   CardType  - Card category (e.g., "Credit", "Debit", "Prepaid")
// These are null for non-card transactions (wire transfers, ACH, etc.).
public record TransactionCreatedEvent(
    string TransactionId,
    string UserId,
    decimal Amount,
    string Currency,
    DateTime Timestamp,
    string? Pan = null,
    string? PinBlock = null,
    string? CardType = null
);
