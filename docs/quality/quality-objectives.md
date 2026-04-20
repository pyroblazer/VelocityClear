# Quality Objectives

**Platform:** VelocityClear Adaptive Real-Time Financial Transaction Platform
**Standard:** ISO 9001:2015 Clause 6.2
**Last Reviewed:** 2026-04-22
**Owner:** Quality Management Team

---

## Overview

This document defines measurable quality objectives for the VelocityClear platform. Each objective is specific, measurable, achievable, relevant, and time-bound (SMART). Objectives are aligned with the quality policy defined in `docs/quality/quality-policy.md` and are reviewed quarterly.

---

## Quality Objectives

### QO-01: Backend Code Coverage

| Property | Value |
|----------|-------|
| **Objective** | Maintain backend code coverage at 80% or above across all microservices |
| **Metric** | Line coverage percentage reported by `dotnet test` with coverage collection enabled |
| **Target** | >= 80% across all services (API Gateway, TransactionService, RiskService, PaymentService, ComplianceService, PinEncryptionService) |
| **Measurement Frequency** | Every CI pipeline run (every push/PR) |
| **Measurement Method** | CI pipeline collects coverage during `backend-unit-tests` and `backend-integration-tests` jobs |
| **Responsible** | Development Team |
| **Reporting** | Coverage summary included in CI job output; trends tracked quarterly |

**Rationale:** High code coverage ensures that the majority of code paths are exercised by automated tests, reducing the risk of regression defects. The 80% target balances thoroughness with pragmatic development velocity.

**Actions to Achieve:**
- Write unit tests for all new code before or alongside implementation
- Use integration tests (WebApplicationFactory) to validate end-to-end service behavior
- Review coverage reports in CI to identify uncovered code paths
- Prioritize test additions for services with coverage below the target

---

### QO-02: Zero Critical/High Vulnerabilities

| Property | Value |
|----------|-------|
| **Objective** | Maintain zero CRITICAL and HIGH severity vulnerabilities in Trivy scan results |
| **Metric** | Count of CRITICAL and HIGH severity findings from Trivy filesystem and IaC configuration scans |
| **Target** | 0 CRITICAL findings, 0 HIGH findings |
| **Measurement Frequency** | Every CI pipeline run (every push/PR) |
| **Measurement Method** | `trivy-scan` job in CI pipeline runs `trivy fs . --severity CRITICAL,HIGH` and `trivy config . --severity CRITICAL,HIGH` |
| **Responsible** | Security & Compliance Team, Development Team |
| **Reporting** | SARIF results uploaded to GitHub Security tab; findings reviewed weekly |

**Rationale:** Security vulnerabilities in dependencies and infrastructure configuration represent the highest-risk attack surface. A zero-tolerance policy for CRITICAL and HIGH findings ensures the platform is not exposed to known exploitable weaknesses.

**Actions to Achieve:**
- Update dependencies promptly when security patches are released
- Evaluate alternative libraries for dependencies with unpatched vulnerabilities
- Run `dotnet audit` and `npm audit` locally before submitting PRs
- Review Trivy SARIF results in the GitHub Security tab weekly

---

### QO-03: API Response Latency

| Property | Value |
|----------|-------|
| **Objective** | All API endpoints respond within 500ms at the 95th percentile (P95) |
| **Metric** | P95 response latency measured by Prometheus metrics and Serilog request logging |
| **Target** | P95 < 500ms for all API endpoints |
| **Measurement Frequency** | Continuous (Prometheus scraping every 15 seconds) |
| **Measurement Method** | `http_request_duration_seconds` histogram metric collected by Prometheus; P95 aggregation via PromQL |
| **Responsible** | Development Team, SRE Team |
| **Reporting** | Grafana dashboard shows real-time latency percentiles; P95 trends reviewed weekly |

**Rationale:** Financial transaction platforms must provide responsive user experiences. Sub-second response times at P95 ensure that the vast majority of users experience immediate feedback when submitting transactions or querying data.

**Actions to Achieve:**
- Optimize database queries and ensure proper indexing via EF Core migration strategies
- Monitor Prometheus `HighLatencyP95` alert for any endpoint exceeding the threshold
- Profile and optimize hot paths in transaction creation and risk evaluation
- Use asynchronous processing for non-critical paths (event publishing, audit logging)

---

### QO-04: Audit Chain Integrity

| Property | Value |
|----------|-------|
| **Objective** | Audit chain verification passes 100% of the time under normal operation |
| **Metric** | Result of `GET /api/audit/verify` endpoint, which walks the SHA-256 hash chain |
| **Target** | 100% pass rate (all hashes valid, no tampering detected) |
| **Measurement Frequency** | Automated check every 6 hours; manual verification after any incident |
| **Measurement Method** | The ComplianceService `VerifyChainAsync()` method recomputes every hash and compares against stored values |
| **Responsible** | Compliance Team, Development Team |
| **Reporting** | Verification result logged and tracked; any failure triggers a P1 incident |

**Rationale:** The audit chain is the platform's tamper-evidence mechanism for financial transaction records. Any chain verification failure indicates potential data tampering and must be treated as a critical security incident.

**Actions to Achieve:**
- Monitor the audit chain verification result as a health check
- Alert immediately on any verification failure
- Ensure the ComplianceService is included in backup procedures
- Never manually modify audit log entries in the database

---

### QO-05: CI Pipeline Duration

| Property | Value |
|----------|-------|
| **Objective** | Complete CI pipeline execution in under 15 minutes |
| **Metric** | Total wall-clock time from pipeline trigger to completion of all jobs |
| **Target** | < 15 minutes end-to-end |
| **Measurement Frequency** | Every CI pipeline run |
| **Measurement Method** | GitHub Actions workflow duration reported in the Actions tab |
| **Responsible** | Development Team, DevOps Team |
| **Reporting** | Pipeline duration tracked per run; trends reviewed monthly |

**Rationale:** Fast CI feedback loops are essential for developer productivity. A 15-minute target ensures that developers receive timely feedback on their changes and can merge with confidence. Longer pipelines discourage frequent commits and delay defect detection.

**Actions to Achieve:**
- Run independent CI jobs in parallel (backend-lint, backend-unit-tests, frontend-build matrix)
- Cache NuGet and npm packages between runs
- Optimize test execution order (fast unit tests first, slower integration tests second)
- Monitor for slow jobs and investigate performance regressions

---

### QO-06: Pull Request Review Requirement

| Property | Value |
|----------|-------|
| **Objective** | All pull requests require at least one reviewer approval before merging |
| **Metric** | Count of PRs merged without at least one approval |
| **Target** | 0 PRs merged without approval |
| **Measurement Frequency** | Continuous (branch protection rule enforcement) |
| **Measurement Method** | GitHub branch protection rule requiring at least 1 approving review |
| **Responsible** | Development Team Lead |
| **Reporting** | Reviewed in monthly development retrospectives |

**Rationale:** Code review is a primary quality gate that catches defects, ensures adherence to coding standards, and promotes knowledge sharing. Mandatory review ensures that no code enters the main branch without scrutiny from at least one other team member.

**Actions to Achieve:**
- Enable GitHub branch protection on `main` requiring at least 1 approving review
- Reviewers should check for correctness, security, performance, and test coverage
- Use the CI pipeline results as part of the review (all checks must pass)
- Encourage thorough reviews with constructive feedback

---

## Objective Status Summary

| ID | Objective | Target | Current Status | Last Measured |
|----|-----------|--------|----------------|---------------|
| QO-01 | Backend Code Coverage | >= 80% | Baseline established | 2026-04-22 |
| QO-02 | Zero Critical/High Vulnerabilities | 0 | Achieved (clean Trivy scan) | 2026-04-22 |
| QO-03 | API Response Latency P95 | < 500ms | Baseline established | 2026-04-22 |
| QO-04 | Audit Chain Integrity | 100% | Achieved | 2026-04-22 |
| QO-05 | CI Pipeline Duration | < 15 min | Baseline established | 2026-04-22 |
| QO-06 | PR Review Requirement | 100% | Enforced via branch protection | 2026-04-22 |

---

## Review and Update

These quality objectives are reviewed:
- Quarterly by the Quality Management Team
- Following any significant quality incident
- When business requirements or technology strategy changes warrant new or revised objectives
- As part of the ISO 9001 management review process

Objectives that are consistently achieved may be made more stringent. Objectives that are consistently missed will be analyzed for root cause and either resourced appropriately or revised with documented justification.

---

## Change Log

| Date | Version | Author | Description |
|------|---------|--------|-------------|
| 2026-04-22 | 1.0 | Quality Management Team | Initial quality objectives |
