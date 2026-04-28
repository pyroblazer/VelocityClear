# ADR-010: OJK KYC/eKYC Workflow

**Status:** Accepted  
**Date:** 2026-04-28  
**Regulation:** POJK No. 12/POJK.01/2017 (Program Anti Pencucian Uang), UU No. 11/2008 (ITE)

## Context

OJK requires all fintech platforms to perform Customer Due Diligence (CDD) before onboarding customers. This includes identity verification, liveness detection, and watchlist screening against PEP/sanction lists.

## Decision

KYC is implemented as a state machine within `ComplianceService`:

```
Pending → InProgress → [Verified | Rejected | Expired | Suspended]
```

**Liveness detection** is simulated with a random confidence score (0.85–0.99). Real integrations (e.g., VIDA, Verihubs) plug in at `KycService.PerformLivenessCheckAsync`. Threshold: ≥ 0.90 confidence to pass.

**Watchlist screening** uses Levenshtein-distance fuzzy matching (≥ 0.80 similarity triggers a hit) against seeded PEP/sanction entries in `WatchlistEntries`. Real integrations connect to Refinitiv World-Check or Dow Jones Risk & Compliance.

**Verified profiles** expire after 2 years per POJK requirements.

## Consequences

- All KYC status changes publish `KycStatusChangedEvent` for audit trail.
- Watchlist hits publish `WatchlistHitDetectedEvent` triggering downstream AML review.
- KYC profiles are retained for 5 years per `AuditRetentionPolicy`.
- `POST /api/kyc/user/{userId}/verified` provides a fast gate-check for transaction services.
