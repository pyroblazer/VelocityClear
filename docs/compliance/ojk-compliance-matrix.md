# OJK Compliance Matrix — VelocityClear

**Last updated:** 2026-04-28

This document maps each OJK regulatory requirement to the implemented features.

---

## 1. Data Protection (UU PDP + POJK No. 6/POJK.07/2022)

| Requirement | Implementation | Endpoint |
|-------------|---------------|---------|
| Personal data classification | `DataClassification` model + `DataMaskingService` | `GET /api/data-masking/classifications` |
| Field-level masking | `DataMaskingService` (Full/Partial/LastFour/Email/Phone) | `POST /api/data-masking/mask` |
| Consent management | `ConsentService` + `ConsentRecord` model | `POST /api/consent/grant`, `/withdraw` |
| Consent withdrawal | `ConsentService.WithdrawConsentAsync` | `POST /api/consent/withdraw` |
| Data retention enforcement | `AuditRetentionPolicy` (seeded: 5–10 years) | — |
| Right to erasure (DataDeletion consent type) | `ConsentType.DataDeletion` | `POST /api/consent/grant` |

---

## 2. KYC/eKYC (POJK No. 12/POJK.01/2017)

| Requirement | Implementation | Endpoint |
|-------------|---------------|---------|
| Customer Due Diligence | `KycService` + `KycProfile` model | `POST /api/kyc/initiate` |
| Identity verification | KYC status workflow (InProgress→Verified) | `PUT /api/kyc/{id}/status` |
| Liveness detection | Simulated (0.85–0.99 confidence) | `POST /api/kyc/{id}/liveness` |
| PEP/Sanction screening | Fuzzy Levenshtein match on `WatchlistEntries` | `POST /api/kyc/{id}/screen` |
| KYC expiry (2-year) | `KycProfile.ExpiresAt` set at verification | — |
| Watchlist hit event | `WatchlistHitDetectedEvent` published | — |

---

## 3. AML/CFT (POJK APU-PMT)

| Requirement | Implementation | Endpoint |
|-------------|---------------|---------|
| Real-time transaction monitoring | `AmlRuleEngine` integrated into `RiskEvaluationService` | (event-driven) |
| Structuring detection | `STRUCTURING` rule (near-threshold clustering) | — |
| Velocity monitoring (1h/24h/7d) | `VELOCITY_1H`, `VELOCITY_24H`, `VELOCITY_7D` rules | — |
| Cross-border reporting | `CROSS_BORDER` rule (>10,000 threshold) | — |
| Dormant account activation | `DORMANT_ACTIVATION` rule | — |
| AML alert management | `AmlMonitoringService` + `AmlAlert` model | `GET /api/aml/alerts` |
| Suspicious Activity Report (SAR) | `SarService` + `SuspiciousActivityReport` model | `POST /api/aml/sar` |
| OJK reference numbering | Auto-generated `SAR-YYYYMMDD-{UUID8}` | — |

---

## 4. Segregation of Duties / Maker-Checker (POJK)

| Requirement | Implementation | Endpoint |
|-------------|---------------|---------|
| Dual-control approval | `ApprovalService` (maker ≠ checker enforced) | `POST /api/approvals`, `/process` |
| Role-based SoD conflict detection | `AccessControlService` (conflicting pair check) | `POST /api/access-control/assign-role` |
| ABAC enforcement | `AccessControlService.EvaluateAbac` | `POST /api/access-control/check` |
| Approval audit trail | `ApprovalRequestedEvent`, `ApprovalCompletedEvent` | — |

---

## 5. Regulatory Reporting (POJK)

| Requirement | Implementation | Endpoint |
|-------------|---------------|---------|
| Monthly/quarterly/annual reports | `ReportingService` (JSON/XML/CSV) | `POST /api/reports` |
| XML report format | `BuildXmlReport` (`<OjkReport>` schema) | `GET /api/reports/{id}/download` |
| 10-year retention | `OjkReport.RetentionYears = 10` (default) | — |
| Immutable report storage | `WormStorageService` (write-once + SHA-256) | — |
| Audit log retention policy | `AuditRetentionPolicy` table (seeded) | — |

---

## 6. Customer Protection / Complaints (POJK Perlindungan Konsumen)

| Requirement | Implementation | Endpoint |
|-------------|---------------|---------|
| Complaint intake | `ComplaintService` + `ComplaintTicket` model | `POST /api/complaints` |
| 20-business-day SLA (POJK art. 39) | `SlaDeadline = CreatedAt + 28 days`, `SlaBreach` flag | `GET /api/complaints/{id}/sla` |
| Escalation workflow | Escalation levels (Level1→Level2→Level3→OJK) | `POST /api/complaints/{id}/escalate` |
| Resolution tracking | `ComplaintTicket.Resolution` + `ResolvedAt` | `POST /api/complaints/{id}/resolve` |

---

## 7. Digital Signature (UU ITE & PSrE)

| Requirement | Implementation | Endpoint |
|-------------|---------------|---------|
| Electronic document signing | `DigitalSignatureService` (HMAC-SHA256) | `POST /api/digital-signature/sign` |
| Signature verification | Document hash + HMAC comparison | `POST /api/digital-signature/verify` |
| Vendor integration readiness | `SignedDocument.VendorReferenceId` field | — |
| 5-year signature retention | `SignedDocument.ExpiresAt = SignedAt + 5y` | — |

---

## 8. SOC / Incident Response (POJK IT Risk)

| Requirement | Implementation | Endpoint |
|-------------|---------------|---------|
| Security incident tracking | `SocService` + `SecurityIncident` model | `POST /api/soc/incidents` |
| Incident severity classification | `IncidentSeverity` (Low/Medium/High/Critical) | — |
| SOC dashboard | `SocService.GetDashboardAsync` | `GET /api/soc/dashboard` |
| Runbook references | `SecurityIncident.RunbookReference` field | — |

---

## 9. DRP/BCP & Cloud Governance (POJK IT Risk Art. 28–35)

| Requirement | Implementation | Endpoint |
|-------------|---------------|---------|
| DRP/BCP plan tracking | `InfrastructureComplianceService` + `DrpBcpStatus` | `GET /api/infrastructure-compliance/drp` |
| RTO/RPO targets | `DrpBcpStatus.RtoMinutes`, `RpoMinutes` | — |
| Data residency verification | `DataResidencyCheck` model | `GET /api/infrastructure-compliance/data-residency` |
| Third-party vendor SLA audit | `VendorAuditEntry` model | `GET /api/infrastructure-compliance/vendors` |

---

## 10. Existing Platform Compliance (pre-OJK plan)

| Requirement | Implementation |
|-------------|---------------|
| JWT authentication | `ApiGateway` — POJK IAM requirements |
| RBAC (Guest/User/Admin/Auditor) | `UserRole` enum + JWT claims |
| SHA-256 audit hash chain | `AuditService` — tamper-evident logs |
| Prometheus metrics | `ComplianceService /metrics` |
| Encrypted PIN (ISO 9564) | `PinEncryptionService` — HSM simulation |
| Risk scoring | `RiskEvaluationService` — 0–100 score |
