# Incident Response Procedure

**Platform:** VelocityClear Adaptive Real-Time Financial Transaction Platform
**Last Reviewed:** 2026-04-22
**Owner:** Operations & SRE Team

---

## Overview

This document defines the incident response procedure for the VelocityClear platform. It establishes severity levels, roles and responsibilities, escalation paths, communication protocols, and the post-incident review process. The goal is to minimize the impact of incidents on platform availability, data integrity, and user experience.

---

## Severity Levels

### P1 - Critical

**Definition:** Complete platform outage or severe degradation affecting all users. Data integrity may be compromised. Financial transactions cannot be processed.

**Criteria (any one suffices):**
- Platform availability drops below 99.5% (measured over any 1-hour window)
- All transactions failing across the platform
- Audit chain verification fails (data tampering detected)
- Confirmed or suspected security breach (data exfiltration, unauthorized access)
- Database corruption or data loss detected
- All backend services simultaneously unavailable

**Response Targets:**
- Acknowledgment: 15 minutes
- Status updates: Every 30 minutes
- Resolution (RTO): 1 hour
- Post-incident review: Within 24 hours

**Examples:**
- SQL Server crash with data corruption
- API Gateway unreachable, all requests timing out
- Audit chain verification returns tampered entries
- JWT signing key compromised

---

### P2 - High

**Definition:** Significant degradation affecting a substantial portion of users or a critical business function. The platform is partially operational but with major limitations.

**Criteria (any one suffices):**
- A single backend service is completely unavailable
- Transaction success rate drops below 95%
- P95 latency exceeds 2,000ms for more than 5 minutes
- A subset of API endpoints returning 5xx errors consistently
- Event bus failure causing events to stop propagating between services

**Response Targets:**
- Acknowledgment: 30 minutes
- Status updates: Every 1 hour
- Resolution: 4 hours
- Post-incident review: Within 48 hours

**Examples:**
- RiskService down, transactions queued but not processed
- Redis connection lost, events not propagating
- ComplianceService unable to write audit logs
- High error rate (5xx) on transaction creation endpoint

---

### P3 - Medium

**Definition:** Minor degradation affecting a limited number of users or a non-critical function. The platform is largely operational.

**Criteria (any one suffices):**
- A single frontend application is unavailable (other frontends unaffected)
- P95 latency exceeds 500ms but remains below 2,000ms
- Individual transactions failing intermittently (success rate above 95%)
- Prometheus alert firing but auto-recovering
- Non-critical monitoring or dashboard issues

**Response Targets:**
- Acknowledgment: 2 hours
- Status updates: Every 4 hours
- Resolution: 24 hours
- Post-incident review: Included in weekly operations review

**Examples:**
- Grafana dashboard not loading
- SSE connections dropping intermittently
- Individual transaction timeout (user can retry successfully)
- Frontend build or deployment failure (service still running previous version)

---

### P4 - Low

**Definition:** Cosmetic issues, minor bugs, or improvements that do not affect platform functionality or user experience in a meaningful way.

**Criteria (any one suffies):**
- UI rendering issues on specific browsers
- Documentation errors or omissions
- Performance optimization opportunities
- Non-critical dependency updates available
- Log formatting issues

**Response Targets:**
- Acknowledgment: Next business day
- Status updates: As needed
- Resolution: Next sprint
- Post-incident review: Not required

**Examples:**
- Incorrect date format in a dashboard
- Typo in an API error message
- Trivy scan reports MEDIUM severity finding
- CSS alignment issue on a specific screen size

---

## Roles and Responsibilities

### Incident Commander (IC)

- The first on-call responder to acknowledge the incident
- Responsible for coordinating the response effort
- Makes decisions on severity level and escalation
- Communicates status to stakeholders
- Ensures the post-incident review is scheduled and completed

### On-Call Responder

- Receives and acknowledges alerts from Prometheus/Grafana
- Performs initial triage and severity assessment
- Executes runbook procedures from `docs/operations/alerting-runbook.md`
- Escalates to the Incident Commander if unable to resolve within the acknowledgment window

### Subject Matter Expert (SME)

- Provides deep technical expertise for specific platform components
- Called in by the Incident Commander when specialized knowledge is required
- Available for backend services (.NET), databases (SQL Server), infrastructure (Docker, Redis, RabbitMQ, Kafka), and security (PIN encryption, audit chain)

### Communications Lead

- Manages external communications during P1 and P2 incidents
- Updates status page (if applicable)
- Notifies affected users and stakeholders
- Coordinates with the Incident Commander on messaging

---

## Escalation Matrix

### Automatic Escalation

| Time After Detection | P1 | P2 | P3 | P4 |
|----------------------|-----|-----|-----|-----|
| 0 min | Alert fires | Alert fires | Alert fires | Logged |
| 5 min | On-call paged | On-call paged | - | - |
| 15 min | **IC + SME engaged** | - | - | - |
| 30 min | **Platform Director notified** | **IC engaged** | On-call notified | - |
| 60 min | **Executive notification** | **SME engaged** | - | - |
| 2 hours | **Continuous update cycle** | **Platform Director notified** | **IC engaged** | - |
| 4 hours | **Post-incident review initiated** | **Executive notification** | - | - |

### Manual Escalation

Any team member can escalate an incident by:
1. Posting in the `#incidents` Slack channel with the severity tag (e.g., `@oncall P1: [description]`)
2. Paging the on-call responder via the on-call rotation system
3. Calling the Incident Commander directly for P1 incidents

---

## Incident Response Workflow

### Phase 1: Detection

Incidents may be detected through:

1. **Automated Alerts:** Prometheus alerting rules fire and send notifications to the on-call rotation
2. **User Reports:** Users report issues via support channels
3. **Monitoring Dashboards:** Team members observe anomalies on Grafana dashboards
4. **CI/CD Failures:** Build or deployment pipeline failures that may affect production

### Phase 2: Triage

Upon detection, the on-call responder performs initial triage:

1. Acknowledge the alert within the target time for the assessed severity
2. Determine the severity level based on the criteria above
3. Identify the affected components (services, databases, networks)
4. Assess the blast radius (how many users/transactions are affected)
5. Check recent deployments for potential root cause
6. Review recent changes: `git log --oneline -20`

### Phase 3: Containment

Take immediate action to prevent the incident from worsening:

1. If a recent deployment caused the issue, consider rollback (see release process)
2. If a single service is failing, isolate it and redirect traffic if possible
3. If the database is under load, consider blocking non-essential traffic
4. If a security breach is suspected, revoke compromised credentials immediately
5. Enable verbose logging if needed for diagnosis: update Serilog log level via configuration

### Phase 4: Resolution

Implement the fix:

1. Follow the appropriate runbook in `docs/operations/alerting-runbook.md`
2. If the runbook does not cover the situation, collaborate with SMEs to develop a solution
3. Test the fix in a non-production environment if time permits
4. Deploy the fix to production following the hotfix procedure if code changes are required
5. Verify that the fix resolves the issue and all health checks pass

### Phase 5: Recovery

Restore the platform to full operational status:

1. Verify all services are healthy
2. Process test transactions to confirm end-to-end functionality
3. Verify audit chain integrity via `GET /api/audit/verify`
4. Monitor metrics closely for 30 minutes after resolution
5. Confirm no alerts are firing
6. Reduce logging verbosity back to normal levels

### Phase 6: Post-Incident Review

After the incident is resolved, conduct a post-incident review (required for P1 and P2, recommended for P3).

---

## Post-Incident Review Template

```markdown
# Post-Incident Review: [INCIDENT-ID]

## Summary
- **Date:** YYYY-MM-DD
- **Duration:** X hours Y minutes
- **Severity:** P[1-4]
- **Incident Commander:** [Name]
- **Affected Services:** [List of services]

## Timeline

| Time (UTC) | Event |
|------------|-------|
| HH:MM | Alert fired: [alert name] |
| HH:MM | Incident acknowledged by [name] |
| HH:MM | Severity assessed as P[level] |
| HH:MM | Root cause identified: [description] |
| HH:MM | Fix deployed: [description] |
| HH:MM | All health checks passing |
| HH:MM | Incident closed |

## Root Cause Analysis

### What happened?
[Detailed description of the technical failure]

### Why did it happen?
[Underlying cause, not just the symptom]

### Why wasn't it prevented?
[What monitoring, testing, or process gap allowed this to reach production]

## Impact

- **Users affected:** [count or percentage]
- **Transactions affected:** [count]
- **Data impact:** [any data loss or corruption]
- **Revenue impact:** [if quantifiable]
- **SLA impact:** [did this breach availability, latency, or other SLA]

## What Went Well

1. [Positive aspect of the response]
2. [Positive aspect of the response]

## What Could Be Improved

1. [Area for improvement]
2. [Area for improvement]

## Action Items

| ID | Action | Owner | Priority | Due Date | Status |
|----|--------|-------|----------|----------|--------|
| AI-01 | [Description] | [Name] | High | YYYY-MM-DD | Open |
| AI-02 | [Description] | [Name] | Medium | YYYY-MM-DD | Open |

## Lessons Learned

[Key takeaways that should be shared with the broader team]

## Attachments

- [Links to relevant logs, dashboards, metrics screenshots]
```

---

## Communication Templates

### Incident Declaration

```
INCIDENT [INCIDENT-ID] - P[LEVEL] - [Brief Description]

Detected: [Timestamp UTC]
Severity: P[Level]
Status: INVESTIGATING

Impact: [Description of user/business impact]
Affected Services: [List]

Incident Commander: [Name]
On-Call Responder: [Name]

Next update in: [X minutes]
```

### Status Update

```
UPDATE [INCIDENT-ID] - P[LEVEL] - [Brief Description]

Time: [Timestamp UTC]
Status: [INVESTIGATING | CONTAINING | RESOLVING | RESOLVED]

Current Status: [What we know now]
Actions Taken: [What we have done]
Next Steps: [What we are doing next]

Next update in: [X minutes]
```

### Resolution

```
RESOLVED [INCIDENT-ID] - P[LEVEL] - [Brief Description]

Time: [Timestamp UTC]
Duration: [X hours Y minutes]
Status: RESOLVED

Root Cause: [Brief description]
Resolution: [What fixed it]
Impact: [Final impact assessment]

Post-incident review scheduled for: [Date/Time]
```

---

## On-Call Rotation

### Schedule

- Primary on-call: Rotates weekly among SRE team members
- Secondary on-call: Backup for the primary, escalated after 15 minutes if primary unresponsive
- Incident Commander: Senior team member, available for P1/P2 escalation

### Expectations

- On-call responders must be able to access production systems within 15 minutes
- On-call responders must have working VPN, SSH keys, and Docker credentials
- On-call responders must acknowledge alerts within the target time for the severity level
- Handoff briefings occur at each rotation change, documenting any ongoing issues

---

## Testing and Drills

- Incident response procedures are tested quarterly via tabletop exercises
- A P1 simulation drill is conducted semi-annually, practicing the full response workflow
- Lessons learned from drills are incorporated into this document and the alerting runbooks

---

## Change Log

| Date | Version | Author | Description |
|------|---------|--------|-------------|
| 2026-04-22 | 1.0 | Operations & SRE Team | Initial incident response procedure |
