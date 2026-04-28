# ADR-013: OJK Regulatory Reporting Templates

**Status:** Accepted  
**Date:** 2026-04-28  
**Regulation:** POJK reporting obligations

## Context

OJK mandates periodic reports (monthly, quarterly, annual) covering transactions, risk events, AML summaries, and suspicious transactions. Reports must be retained for 10 years.

## Decision

`ReportingService` generates reports from live `ComplianceDbContext` data in three formats:

| Format | Use case |
|--------|----------|
| JSON | Machine-readable API responses |
| XML | OJK submission format (structured `<OjkReport>` schema) |
| CSV | Spreadsheet-compatible summary |

**Report types:**

| Type | Content |
|------|---------|
| MonthlyTransaction | Audit log counts + event type breakdown |
| QuarterlyRisk | AML alert summary + severity distribution |
| AnnualCompliance | Full-year aggregate |
| AmlSummary | AML-specific alert details |
| SuspiciousTransaction | SAR-linked transactions |

**WORM storage:** After generation, report content is stored in `WormStorageService` (write-once dictionary) with SHA-256 integrity hash. Overwrite attempts throw `InvalidOperationException`. This simulates immutable storage (OJK POJK IT Risk art. 28).

## Consequences

- Reports are generated synchronously. For large datasets, this should move to a background job.
- Content is stored as a string in `OjkReport.Content`. Large CSV/XML reports should use blob storage in production.
- Retention is tracked via `OjkReport.RetentionYears` (default 10 years).
