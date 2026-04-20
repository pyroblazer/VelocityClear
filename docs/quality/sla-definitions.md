# SLA Definitions

**Platform:** VelocityClear Adaptive Real-Time Financial Transaction Platform
**Last Reviewed:** 2026-04-22
**Owner:** Operations & SRE Team

---

## Overview

This document defines the Service Level Agreements (SLAs) for the VelocityClear platform. SLAs establish measurable commitments for platform availability, performance, recovery, and support responsiveness. These definitions serve as the basis for operational monitoring, alerting, and incident prioritization.

---

## SLA Summary

| Metric | Target | Measurement Window | Measurement Method |
|--------|--------|--------------------|--------------------|
| Availability | 99.9% | 30-day rolling | Prometheus uptime metrics |
| Latency (P95) | < 500ms | Real-time | Prometheus histogram |
| Latency (P99) | < 2,000ms | Real-time | Prometheus histogram |
| Recovery Time Objective (RTO) | < 1 hour | Per incident | Incident timestamps |
| Recovery Point Objective (RPO) | < 5 minutes | Per incident | Last backup timestamp |
| P1 Incident Response | < 15 min acknowledgment | Per incident | Incident ticket timestamps |

---

## SLA Definitions

### 1. Availability

**Definition:** The percentage of time the platform is operational and capable of processing transactions within the defined latency targets, measured over a rolling 30-day window.

**Formula:**
```
Availability = (Total Minutes in Window - Downtime Minutes) / Total Minutes in Window * 100
```

**Target:** 99.9% (maximum 43.2 minutes downtime per 30-day window)

**Inclusions:**
- All six backend microservices (API Gateway, TransactionService, RiskService, PaymentService, ComplianceService, PinEncryptionService)
- Database connectivity (SQL Server)
- Event bus connectivity (Redis, when configured)
- SSE stream availability

**Exclusions:**
- Pre-announced scheduled maintenance windows (maximum 1 hour per month, must be communicated 48 hours in advance)
- Force majeure events (natural disasters, cloud provider regional outages)
- Failures in upstream systems outside the platform's control (e.g., external payment processor outages)
- Downtime caused by customer-side network issues

**Measurement:**
- Prometheus scrapes each service's health endpoint (`/health`) every 15 seconds
- Alert `ServiceDown` fires if any health check fails for more than 60 seconds
- Uptime percentage is calculated daily and aggregated over the rolling 30-day window
- Grafana dashboard displays current availability percentage and trend

**Escalation on Breach:**
- Availability drops below 99.9%: P2 incident, root cause analysis required within 48 hours
- Availability drops below 99.5%: P1 incident, immediate escalation to platform leadership
- Availability drops below 99.0%: P1 incident, executive notification, post-incident review required within 24 hours

---

### 2. Latency

#### 2a. Transaction Creation Latency (P95)

**Definition:** The time from when the API Gateway receives a `POST /api/transactions` request to when the response is returned to the client, measured at the 95th percentile.

**Target:** P95 < 500ms

**Measurement:**
- Prometheus `http_request_duration_seconds` histogram with `label: {method="POST", path="/api/transactions"}`
- P95 calculated via PromQL: `histogram_quantile(0.95, rate(http_request_duration_seconds_bucket[5m]))`
- Alert `HighLatencyP95` fires when P95 exceeds 500ms for more than 5 minutes

**Scope:**
- Includes request parsing, JWT validation, database write, event publishing, and response serialization
- Excludes network latency between the client and the API Gateway (measured server-side)

#### 2b. Transaction Creation Latency (P99)

**Definition:** The time from when the API Gateway receives a `POST /api/transactions` request to when the response is returned to the client, measured at the 99th percentile.

**Target:** P99 < 2,000ms (2 seconds)

**Measurement:**
- Same Prometheus metric as P95, calculated at the 99th percentile
- Provides an upper bound on worst-case response times

**Escalation on Breach:**
- P95 exceeds 500ms for more than 5 minutes: P3 incident, auto-alert to SRE team
- P99 exceeds 2,000ms for more than 5 minutes: P2 incident, immediate investigation
- Any single request exceeding 10,000ms (10 seconds): logged and investigated as a P3 incident

---

### 3. Recovery Time Objective (RTO)

**Definition:** The maximum acceptable time to restore platform functionality after a service disruption.

**Target:** RTO < 1 hour

**Scope:**
- Applies to all platform components: backend services, databases, event bus, frontend applications
- Restoration means all health checks are passing and transactions can be processed successfully

**Recovery Procedures by Component:**

| Component | Recovery Method | Estimated Time |
|-----------|----------------|----------------|
| Backend Service (single) | Docker container restart | 1-3 minutes |
| SQL Server | Docker container restart with volume mount | 2-5 minutes |
| Redis | Docker container restart | 1-2 minutes |
| RabbitMQ | Docker container restart with volume mount | 2-5 minutes |
| Full Platform | `docker-compose down && docker-compose up -d` | 5-10 minutes |
| Database Corruption | Restore from latest backup | 10-30 minutes |

**Measurement:**
- RTO is measured from the time the incident is declared to the time all health checks pass
- Incident timestamps are recorded in the incident ticket

---

### 4. Recovery Point Objective (RPO)

**Definition:** The maximum acceptable amount of data loss measured in time, representing the point in time to which data can be recovered after a disruption.

**Target:** RPO < 5 minutes

**Scope:**
- Applies to all persistent data stores: TransactionService database, ComplianceService audit log database
- Does not apply to in-memory state (Redis cache, RiskService velocity tracking, PaymentService running totals) which is documented as ephemeral

**Achieved By:**
- SQL Server full backups every 4 hours (scheduled via `sqlcmd` or SQL Server Agent)
- SQL Server transaction log backups every 5 minutes (when in full recovery mode)
- Docker volume persistence ensures data survives container restarts
- Event sourcing pattern via audit log provides a reconstructable record of all state changes

**Measurement:**
- RPO is measured as the time between the last successful backup and the disruption event
- Backup timestamps are logged and monitored

---

### 5. Incident Response Time

**Definition:** The time from when an incident is detected (alert fires) to when the on-call responder acknowledges the incident.

**Target:** P1 incidents acknowledged within 15 minutes

**Severity-Based Response Targets:**

| Severity | Acknowledgment | Status Update | Resolution |
|----------|---------------|---------------|------------|
| P1 - Critical | 15 minutes | Every 30 minutes | < 1 hour (RTO) |
| P2 - High | 30 minutes | Every 1 hour | < 4 hours |
| P3 - Medium | 2 hours | Every 4 hours | < 24 hours |
| P4 - Low | Next business day | As needed | Next sprint |

**P1 Criteria:**
- Platform availability drops below 99.5%
- Audit chain verification fails
- Security breach confirmed or suspected
- Data loss or data corruption detected
- All transactions failing (complete service outage)

**Measurement:**
- Alert firing timestamp to responder acknowledgment timestamp
- Acknowledgment via incident management system (e.g., PagerDuty, Slack incident channel)

---

## SLA Monitoring and Reporting

### Real-Time Monitoring

- **Grafana Dashboards:** Real-time visualization of availability, latency percentiles, and error rates
- **Prometheus Alerts:** Automated alerts for SLA threshold breaches as defined in the alerting rules
- **SSE Stream:** Frontend dashboards display live transaction and event data

### Periodic Reporting

| Report | Frequency | Audience | Content |
|--------|-----------|----------|---------|
| SLA Dashboard | Continuous | All stakeholders | Real-time SLA metrics |
| Weekly SLA Summary | Weekly | Operations Team | Availability %, latency percentiles, incident count |
| Monthly SLA Report | Monthly | Platform Leadership | SLA compliance, trends, improvement actions |
| Quarterly SLA Review | Quarterly | Executive Sponsors | SLA performance, contract compliance, strategic adjustments |

---

## SLA Exclusions and Credits

### Maintenance Windows

One scheduled maintenance window per month, not exceeding 1 hour, is excluded from availability calculations. Maintenance must be:
- Announced at least 48 hours in advance
- Scheduled during off-peak hours (Sunday 02:00-06:00 UTC)
- Completed within the announced window

### Measurement Errors

Brief availability dips (under 60 seconds) caused by health check measurement intervals or transient network blips that self-recover without manual intervention are excluded from SLA calculations.

---

## Review and Amendment

These SLA definitions are reviewed:
- Quarterly as part of the SLA review process
- When platform architecture changes may affect SLA achievability
- Following any SLA breach that reveals an unrealistic or outdated target
- At the request of any stakeholder

Amendments require approval from the Platform Director and must be communicated to all affected parties at least 30 days before taking effect.

---

## Change Log

| Date | Version | Author | Description |
|------|---------|--------|-------------|
| 2026-04-22 | 1.0 | Operations & SRE Team | Initial SLA definitions |
