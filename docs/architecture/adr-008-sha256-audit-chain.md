# ADR-008: SHA-256 Hash Chain for Tamper-Evident Audit Logging

**Status:** Accepted
**Date:** 2026-04-22
**ISO 27001 Controls:** A.12.4.1 (Event logging), A.12.4.2 (Protection of log information), A.12.4.3 (Administrator logs)
**ISO 9001 Clauses:** 7.5

## Context

Financial platforms are subject to regulatory requirements including SOX (Sarbanes-Oxley Act), PCI-DSS (Payment Card Industry Data Security Standard), and AML (Anti-Money Laundering) regulations. These require that audit logs are not just collected but are protected against tampering. An auditor must be able to verify that no log entry has been inserted, modified, or deleted since it was written.

Traditional database-backed audit logs can be modified by anyone with database access (including database administrators). Timestamps can be altered. Entries can be deleted. Without a cryptographic integrity mechanism, there is no way to prove that the audit trail is complete and unmodified.

## Decision

We implemented a sequential SHA-256 hash chain for the ComplianceService audit log. Each audit entry includes a hash that is computed from the current entry's payload and the previous entry's hash:

```
Hash(n) = SHA256(Payload(n) + Hash(n-1))
```

The first entry in the chain uses an all-zeros previous hash (`0000000000000000000000000000000000000000000000000000000000000000`) as its starting point. Each subsequent entry chains to its predecessor, creating a linked sequence where modifying any single entry invalidates every hash that follows.

The chain is verified by the `GET /api/audit/verify` endpoint, which walks the audit log in chronological order, recomputes each hash from its payload and the predecessor's hash, and compares the computed hash against the stored hash. Any mismatch indicates tampering.

## Consequences

**Benefits:**
- SHA-256 is a NIST-approved cryptographic hash function (FIPS 180-4) and is compliant with PCI-DSS requirement 3.4 and PCI-DSS requirement 10.5 for protecting audit trails.
- Any modification to a single log entry (changing the amount, user, or timestamp) invalidates its hash and every subsequent hash in the chain. Tampering is detectable immediately.
- The verification endpoint provides a one-click integrity check that regulators and auditors can use to validate the entire audit trail without understanding the internal implementation.
- Hash computation is deterministic and fast. SHA-256 of a typical audit payload (a few hundred bytes) completes in microseconds.
- The chain structure provides temporal ordering: each entry's hash depends on all prior entries, proving not just integrity but also that no entries have been inserted or deleted.

**Trade-offs:**
- SHA-256 was chosen over SHA-1 because SHA-1 is deprecated due to demonstrated collision attacks (SHAttered, 2017). While preimage attacks against SHA-1 remain impractical, regulatory bodies explicitly discourage its use for new systems.
- SHA-256 was chosen over SHA-384/SHA-512 because the additional security margin is unnecessary for audit log integrity. SHA-256 produces a 32-byte hash that is efficient to store and index, while SHA-512 produces a 64-byte hash with no practical security benefit for this use case.
- The chain is sequential: hash computation for entry N depends on entry N-1. This prevents parallel hash computation for bulk verification. For millions of entries, verification is O(n) and sequential.
- The hash chain detects tampering but does not prevent it. A determined attacker with database access could recompute the entire chain from a modified entry forward. Mitigation: store the latest hash externally (e.g., in a write-once store or blockchain) periodically.
- Inserting entries at the end of the chain is always possible (the attacker computes the next hash). To detect this, the chain should be anchored periodically to an external timestamping service.

**Risks:**
- If the database is compromised and the attacker recomputes the entire chain, tampering is undetectable from the chain alone. The platform should periodically publish the latest chain hash to an append-only external store (e.g., a public blockchain or a WORM storage device) to provide an independent anchor.
