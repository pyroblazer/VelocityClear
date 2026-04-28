# ADR-012: Maker-Checker (Dual Control) Approval

**Status:** Accepted  
**Date:** 2026-04-28  
**Regulation:** POJK Segregation of Duties requirements

## Context

OJK requires segregation of duties for sensitive operations. No single individual should be able to both initiate and approve a sensitive action.

## Decision

`ApprovalService` enforces the maker-checker invariant:

```
approver.UserId ≠ requester.UserId  (enforced in ProcessApprovalAsync)
```

**Operations requiring approval:**

| Operation | ApprovalType |
|-----------|-------------|
| Create new user account | UserCreation |
| Assign/change role | RoleChange |
| File a SAR | SarFiling |
| Generate regulatory report | ReportGeneration |

**Expiry:** Approval requests expire after 24 hours if not processed.

**Role conflict detection (SoD):** `AccessControlService` prevents assigning roles that conflict:

| Conflicting pair |
|-----------------|
| Maker / Checker |
| Trader / RiskOfficer |
| Admin / Auditor |

## Consequences

- Maker-checker violations throw `InvalidOperationException` — callers receive HTTP 400.
- Approval events (`ApprovalRequestedEvent`, `ApprovalCompletedEvent`) are published for audit trail.
- `ApprovalRequest.ProcessedAt` is set on approval/rejection; `ExpiresAt` is checked by consumers.
