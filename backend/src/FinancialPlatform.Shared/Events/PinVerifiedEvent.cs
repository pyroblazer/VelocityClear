// ============================================================================
// PinVerifiedEvent.cs - PIN Verification Result Domain Event
// ============================================================================
// This event is published by the PIN Encryption Service after it processes a
// card transaction (one that carries a PAN and PIN block). It flows into the
// payment authorization pipeline: if PIN verification failed, the Payment
// Service rejects the transaction regardless of risk score.
//
// Event flow for card transactions:
//   TransactionCreated ──► PinVerified ──┐
//           │                            ├─► PaymentAuthorized ──► AuditLogged
//           └──► RiskEvaluated ──────────┘
//
// Non-card transactions (wire transfers, etc.) do not produce this event.
// ============================================================================

namespace FinancialPlatform.Shared.Events;

// "record" creates an immutable reference type with value-based equality.
// Positional parameters become init-only properties (set at construction only).
// "bool Verified" is true if the PIN was successfully decrypted and valid.
public record PinVerifiedEvent(
    string TransactionId,
    string Pan,
    bool Verified,
    string Message,
    DateTime VerifiedAt
);
