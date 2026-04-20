# ISO 27001 Annex A Controls Matrix

**Platform:** VelocityClear Adaptive Real-Time Financial Transaction Platform
**Standard:** ISO/IEC 27001:2022 Annex A
**Last Reviewed:** 2026-04-22
**Owner:** Security & Compliance Team

---

## Overview

This document maps each applicable ISO 27001 Annex A control to its implementation within the VelocityClear platform. The platform is composed of six backend microservices (.NET 10), six frontend applications (React + Vite), and supporting infrastructure (SQL Server, Redis, RabbitMQ, Kafka, Prometheus, Grafana) orchestrated via Docker Compose.

---

## Controls Mapping

### A.5 Organizational Controls

| Control | Description | Implementation | Evidence |
|---------|-------------|----------------|----------|
| A.5.1.1 | Information security policy | Security documentation maintained in `docs/security/` directory. The `CLAUDE.md` file in the repository root documents security architecture, authentication mechanisms, and operational procedures. All team members have access to this documentation via the version-controlled repository. | `docs/security/`, `CLAUDE.md` |

### A.9 Access Control

| Control | Description | Implementation | Evidence |
|---------|-------------|----------------|----------|
| A.9.1.2 | Access to networks and services | All services enforce JWT authentication. Docker Compose defines `backend-net` with `internal: true`, preventing direct external access to backend services. The API Gateway (port 5000) is the sole externally reachable entry point. Frontend applications are served on a separate `frontend-net`. | `src/FinancialPlatform.ApiGateway/`, `infrastructure/docker-compose.yml` |
| A.9.2.1 | Access control policy | Role-based authorization enforced via `[Authorize(Roles = "...")]` attributes on all controller endpoints. Roles include Admin and standard User. Each endpoint specifies the minimum role required for access. | Controller files in each service |
| A.9.2.2 | User provisioning | Registration endpoint (`POST /api/auth/register`) accepts username, password, and optional role. New users are assigned a default role unless an Admin specifies otherwise. User records are stored in the TransactionDbContext Users table. | `src/FinancialPlatform.ApiGateway/Controllers/AuthController.cs` |
| A.9.2.4 | Management of secret authentication information | Passwords are hashed using BCrypt with a work factor of 12. No plaintext passwords are stored in the database or log files. The BCrypt hash includes an embedded salt, ensuring unique hashes per user. | `src/FinancialPlatform.ApiGateway/Services/JwtService.cs` |
| A.9.4.1 | Information access restriction | JWT tokens carry role claims. Each controller endpoint validates the caller's role before processing requests. Endpoints that modify data (POST, PUT, DELETE) require elevated roles. Read-only endpoints may be accessible to authenticated users with lower privilege. | `[Authorize]` attributes on all controllers |
| A.9.4.2 | Secure log-on procedures | JWT tokens are issued with a 1-hour expiry. Each token contains a unique JTI (JWT ID) claim to prevent replay attacks. Tokens are signed with HMAC-SHA256 using a server-side secret key. The login endpoint enforces rate limiting (5 requests per minute). | `src/FinancialPlatform.ApiGateway/Services/JwtService.cs` |
| A.9.4.3 | Password management | BCrypt with work factor 12 is used for all password hashing. The work factor is configurable for future adjustment as hardware improves. Passwords are never logged, displayed in error messages, or transmitted in plaintext after the initial HTTPS request. | `src/FinancialPlatform.ApiGateway/Services/JwtService.cs` |

### A.10 Cryptography

| Control | Description | Implementation | Evidence |
|---------|-------------|----------------|----------|
| A.10.1.1 | Use of cryptographic controls | The platform employs multiple cryptographic mechanisms: (1) JWT tokens signed with HMAC-SHA256 for authentication integrity, (2) AES-256-CBC encryption for HSM key storage and PIN block operations, (3) Triple DES (3DES) for PIN block formatting per ISO 9564, (4) SHA-256 hash chaining for tamper-evident audit logs. All cryptographic operations use .NET's `System.Security.Cryptography` library, which is FIPS 140-2 validated on Windows. | `src/FinancialPlatform.PinEncryptionService/`, `src/FinancialPlatform.ComplianceService/`, `src/FinancialPlatform.ApiGateway/Services/JwtService.cs` |

### A.12 Operations Security

| Control | Description | Implementation | Evidence |
|---------|-------------|----------------|----------|
| A.12.1.1 | Operational procedures | CI/CD pipeline (GitHub Actions) enforces build, test, lint, and security scan stages. Prometheus alerting rules monitor service health. Runbooks are maintained in `docs/operations/alerting-runbook.md` for incident response. Infrastructure is defined as code in `infrastructure/docker-compose.yml`. | `.github/workflows/ci.yml`, `infrastructure/prometheus.yml`, `docs/operations/` |
| A.12.4.1 | Event logging | All services emit structured logs via Serilog to stdout. Docker captures stdout for centralized log aggregation. The ComplianceService maintains a tamper-evident audit log with SHA-256 hash chaining: `Hash(n) = SHA256(Payload(n) + Hash(n-1))`. The audit chain can be verified at `GET /api/audit/verify`. | `src/FinancialPlatform.ComplianceService/`, Serilog configuration in `appsettings.json` per service |
| A.12.4.2 | Administrator and operator logs | Request logging middleware (`UseSerilogRequestLogging()`) emits one structured log line per HTTP request including method, path, status code, and elapsed time. Authentication events (login success, login failure, token refresh) are logged with the requesting user's identity. | Middleware in each service's `Program.cs` |
| A.12.4.3 | Synchronisation of clocks | All timestamp fields across all services use `DateTime.UtcNow` (UTC). No service uses local time. Database columns store UTC timestamps. This ensures consistent chronological ordering of events and audit log entries across distributed services. | All service code using `DateTime.UtcNow` |
| A.12.6.1 | Management of technical vulnerabilities | Trivy vulnerability scanning runs on every push/PR in CI, scanning both filesystem and IaC configurations for CRITICAL and HIGH severity findings. Rate limiting middleware protects against brute-force attacks. CycloneDX SBOM generation provides a full software bill of materials. | `.github/workflows/ci.yml` (trivy-scan job) |

### A.13 Communications Security

| Control | Description | Implementation | Evidence |
|---------|-------------|----------------|----------|
| A.13.1.1 | Network controls | Docker Compose defines isolated networks: `backend-net` (internal, no external routing) and `frontend-net`. The API Gateway is the only service with ports published to the host. All inter-service communication occurs over the isolated `backend-net`. | `infrastructure/docker-compose.yml` |
| A.13.1.3 | Segregation in networks | The platform enforces network segregation via two Docker networks. `frontend-net` connects frontend applications to the API Gateway only. `backend-net` (with `internal: true`) connects all backend microservices and data stores. Frontend applications cannot directly reach backend services other than the API Gateway. | `infrastructure/docker-compose.yml` network definitions |

### A.14 System Development and Maintenance

| Control | Description | Implementation | Evidence |
|---------|-------------|----------------|----------|
| A.14.1.2 | Securing development | The CI pipeline enforces `dotnet format --verify-no-changes` for code style compliance. All changes must pass the full CI pipeline (build, unit tests, integration tests, Newman API tests, Trivy scans) before merging. Code review is required via pull request. | `.github/workflows/ci.yml` (backend-lint, backend-unit-tests, backend-integration-tests jobs) |

### A.15 Supplier Relationships

| Control | Description | Implementation | Evidence |
|---------|-------------|----------------|----------|
| A.15.1.1 | Information security in supplier relationships | CycloneDX SBOM generation produces a complete software bill of materials. Trivy scans evaluate all third-party dependencies (NuGet and npm packages) for known vulnerabilities. NuGet and npm audit commands can be run locally to identify additional dependency risks. All dependency versions are pinned in `.csproj` files and `package-lock.json`. | `.csproj` files across all services, `package-lock.json` in each frontend app |

### A.16 Information Security Incident Management

| Control | Description | Implementation | Evidence |
|---------|-------------|----------------|----------|
| A.16.1.1 | Responsibilities and procedures | Incident response procedures are documented in `docs/operations/incident-response.md`. The procedure defines four severity levels (P1 through P4), escalation paths, response time targets, and a post-incident review template. All team members are expected to be familiar with the procedure. | `docs/operations/incident-response.md` |

### A.18 Compliance

| Control | Description | Implementation | Evidence |
|---------|-------------|----------------|----------|
| A.18.1.1 | Identification of applicable legislation | The platform is designed with consideration for PCI-DSS (payment card industry data security standard), SOX (Sarbanes-Oxley Act for financial reporting controls), and GDPR (General Data Protection Regulation for personal data processing). Specific controls mapped: PCI-DSS Requirement 3 (protect stored cardholder data via AES-256), PCI-DSS Requirement 7 (restrict access via role-based authorization), PCI-DSS Requirement 10 (track and monitor via audit hash chain). | Architecture documentation, controls mapping in this document |

---

## Control Implementation Summary

| Category | Total Controls Mapped | Fully Implemented | Partially Implemented | Planned |
|----------|-----------------------|-------------------|-----------------------|---------|
| A.5 Organizational | 1 | 1 | 0 | 0 |
| A.9 Access Control | 6 | 6 | 0 | 0 |
| A.10 Cryptography | 1 | 1 | 0 | 0 |
| A.12 Operations Security | 5 | 5 | 0 | 0 |
| A.13 Communications Security | 2 | 2 | 0 | 0 |
| A.14 System Development | 1 | 1 | 0 | 0 |
| A.15 Supplier Relationships | 1 | 1 | 0 | 0 |
| A.16 Incident Management | 1 | 1 | 0 | 0 |
| A.18 Compliance | 1 | 1 | 0 | 0 |
| **Total** | **19** | **19** | **0** | **0** |

---

## Review Schedule

This controls matrix is reviewed:
- Quarterly by the Security and Compliance Team
- After any significant architecture change
- Following a security incident
- Prior to ISO 27001 surveillance or recertification audits

---

## Change Log

| Date | Version | Author | Description |
|------|---------|--------|-------------|
| 2026-04-22 | 1.0 | Security & Compliance Team | Initial controls mapping |
