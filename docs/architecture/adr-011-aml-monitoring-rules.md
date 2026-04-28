# ADR-011: AML Monitoring Rules

**Status:** Accepted  
**Date:** 2026-04-28  
**Regulation:** POJK APU-PMT (Anti Pencucian Uang & Pencegahan Pendanaan Terorisme)

## Context

OJK mandates real-time AML monitoring, Suspicious Activity Report (SAR) filing within prescribed timelines, and retention of all AML records for 5+ years.

## Decision

### Rule Engine (`AmlRuleEngine` — Singleton in RiskService)

Six pure-function rules evaluated per transaction:

| Rule | Condition | Severity |
|------|-----------|----------|
| `STRUCTURING` | 3+ transactions between IDR 8M–10M in 3 days | High |
| `VELOCITY_1H` | >10 transactions in 1 hour | Medium |
| `VELOCITY_24H` | >50 transactions in 24 hours | High |
| `VELOCITY_7D` | >200 transactions in 7 days | Medium |
| `ROUND_AMOUNT` | Amount ≥ 1,000 and divisible by 1,000 | Low |
| `CROSS_BORDER` | Cross-border AND amount > 10,000 | Medium |
| `DORMANT_ACTIVATION` | Dormant account AND amount > 5,000 | High |

Rules are integrated into `RiskEvaluationService.EvaluateAsync()` — triggered flags appear in `RiskEvaluatedEvent.Flags`.

### Alert Lifecycle (`AmlMonitoringService` — ComplianceService)

`Open → UnderReview → [Closed | FalsePositive | Escalated]`

### SAR Filing (`SarService` — ComplianceService)

`Draft → Filed → Acknowledged → Closed`

OJK reference numbers auto-generated: `SAR-YYYYMMDD-{8-char UUID}`.

## Consequences

- The `AmlRuleEngine` holds in-memory 7-day transaction history — restart loses it. A future enhancement would persist this to Redis.
- SARs must be filed within 3 business days of detection per POJK APU-PMT.
- All AML records retained 5 years minimum.
