// ============================================================================
// AuditLog.cs - Audit Log Entity Model
//
// This file defines the AuditLog entity, which forms an immutable, tamper-evident
// audit trail for the platform. Each audit log entry records an event and is
// cryptographically chained to the previous entry using SHA-256 hashing - similar
// to a simplified blockchain. This ensures that if any entry is modified, the
// chain breaks and tampering is detectable.
//
// The ComplianceService creates and verifies these audit log entries.
// ============================================================================

namespace FinancialPlatform.Shared.Models;

public class AuditLog
{
    // Unique identifier for this audit log entry.
    public string Id { get; set; } = string.Empty;

    // The type of event being audited (e.g., "TransactionCreated", "RiskEvaluated").
    public string EventType { get; set; } = string.Empty;

    // The full event data serialized as a JSON string. Storing as a string
    // allows any event type to be logged without schema changes.
    public string Payload { get; set; } = string.Empty;

    // SHA-256 hash of this log entry, computed from the entry's own data.
    // This is the "current hash" that links this entry into the chain.
    public string Hash { get; set; } = string.Empty;

    // "string?" - the nullable reference type (can be null). The first entry
    // in the chain has no predecessor, so PreviousHash is null. All subsequent
    // entries store the hash of the previous entry, forming the chain.
    public string? PreviousHash { get; set; }

    // Timestamp of when this audit entry was created.
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
