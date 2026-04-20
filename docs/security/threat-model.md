# STRIDE Threat Model

**Platform:** VelocityClear Adaptive Real-Time Financial Transaction Platform
**Methodology:** STRIDE (Microsoft Threat Modeling Framework)
**Last Reviewed:** 2026-04-22
**Owner:** Security & Compliance Team

---

## Scope

This threat model covers the VelocityClear platform, including the six backend microservices (.NET 10), six frontend applications (React + Vite), Docker infrastructure, and all inter-service communication paths. The model identifies threats across the six STRIDE categories and documents the mitigations implemented for each.

---

## System Boundaries

### Trust Boundaries

1. **Internet to API Gateway** - External untrusted network to the authenticated entry point
2. **API Gateway to Backend Services** - Internal Docker network (`backend-net`, `internal: true`)
3. **Frontend to API Gateway** - Browser to server communication over `frontend-net`
4. **Backend Services to Data Stores** - SQL Server, Redis connections within `backend-net`
5. **PIN Encryption Service to Simulated HSM** - Internal key management boundary
6. **CI/CD Pipeline to Production** - Build artifacts deployed via Docker images

### Data Flows

```
Browser --> API Gateway --> TransactionService --> EventBus --> RiskService
                               |                                  |
                               +--> EventBus --> PaymentService <--+
                               |                                  |
                               +--> EventBus --> ComplianceService (audit log)
                               |
                               +--> EventBus --> PinEncryptionService --> PaymentService
```

---

## Threat Analysis

### S - Spoofing

**Threat:** An attacker attempts to impersonate a legitimate user or service to gain unauthorized access.

| ID | Threat Description | Impact | Mitigation | Residual Risk |
|----|-------------------|--------|------------|---------------|
| S-01 | Attacker steals or forges a JWT token to impersonate an authenticated user | High | JWT tokens are signed with HMAC-SHA256 using a server-side secret key (`Jwt__SecretKey` environment variable). The signature is verified on every request. Token forgery requires knowledge of the signing key. | Low - requires compromise of the JWT secret |
| S-02 | Attacker replays a previously valid JWT token after it has been used | Medium | Each JWT token contains a unique JTI (JWT ID) claim. Tokens expire after 1 hour. Replay prevention is enforced by the token expiry mechanism. | Low |
| S-03 | Attacker spoofs a backend service identity to intercept inter-service traffic | High | All backend services operate within the isolated `backend-net` Docker network (`internal: true`). No external routing is possible. Service-to-service communication does not cross trust boundaries. | Low - requires Docker host compromise |
| S-04 | Attacker registers with a forged identity to obtain elevated privileges | High | The registration endpoint assigns a default (non-admin) role. Admin role assignment requires an existing admin to modify the user's role. Role claims are embedded in the JWT and verified server-side. | Low |

### T - Tampering

**Threat:** An attacker modifies data in transit or at rest to alter transaction details, risk scores, or audit records.

| ID | Threat Description | Impact | Mitigation | Residual Risk |
|----|-------------------|--------|------------|---------------|
| T-01 | Attacker modifies transaction details (amount, currency) in transit | Critical | TLS is recommended for all production deployments. Within the Docker network, traffic is isolated. JWT tokens protect request integrity via HMAC-SHA256 signatures. | Low with TLS, Medium without TLS |
| T-02 | Attacker modifies audit log entries to cover unauthorized activity | Critical | The ComplianceService maintains a SHA-256 hash chain: `Hash(n) = SHA256(Payload(n) + Hash(n-1))`. The first entry uses an all-zeros previous hash. Any modification to a single entry invalidates all subsequent hashes. Integrity verification is available at `GET /api/audit/verify`. | Very Low |
| T-03 | Attacker modifies risk scores or payment decisions via event bus interception | High | The event bus operates within the isolated `backend-net`. In production with Redis/Kafka, event integrity is protected by the transport layer. Events carry structured payloads that are validated by consumers. | Low |
| T-04 | Attacker tampers with the HSM key store to weaken PIN encryption | Critical | The PIN Encryption Service stores the Local Master Key (LMK) as an environment variable (`Hsm__LmkHex`). Derived keys are generated per-operation using AES-256. The key store is in-memory and never persisted to disk. | Low - requires container compromise |

### R - Repudiation

**Threat:** A user or service denies having performed an action, with no evidence to prove otherwise.

| ID | Threat Description | Impact | Mitigation | Residual Risk |
|----|-------------------|--------|------------|---------------|
| R-01 | User denies performing a transaction | High | All transactions are logged with the user ID from the JWT subject claim. The audit trail in ComplianceService records every event (TransactionCreated, RiskEvaluated, PaymentAuthorized, PinVerified) with timestamps, user IDs, and full payloads. | Very Low |
| R-02 | Administrator denies performing a privileged action | High | All administrative actions are logged via Serilog request logging middleware. Authentication events (login, token issuance) are recorded. The JWT subject claim uniquely identifies the acting user. | Very Low |
| R-03 | Service denies processing an event | Medium | The event-driven architecture creates a complete chain of custody: TransactionCreated, RiskEvaluated, PinVerified, PaymentAuthorized, AuditLogged. Each event is recorded in the audit log with the originating service identity. | Low |

### I - Information Disclosure

**Threat:** Sensitive data (PINs, card numbers, transaction details) is exposed to unauthorized parties.

| ID | Threat Description | Impact | Mitigation | Residual Risk |
|----|-------------------|--------|------------|---------------|
| I-01 | Attacker intercepts PIN block data in transit | Critical | PIN blocks are encrypted using ISO 9564 Format 0 with Triple DES. The encrypted PIN block is transmitted over the event bus within the isolated Docker network. The HSM decrypts using the LMK (AES-256-CBC). Decrypted PIN values are never logged, stored, or transmitted outside the HSM boundary. | Low |
| I-02 | Attacker accesses PAN (Primary Account Number) data | Critical | PAN data is carried in TransactionCreatedEvent and processed by the PinEncryptionService. PANs are not persisted in the audit log. Role-based access control restricts transaction data access to authorized users. | Low |
| I-03 | Attacker reads JWT signing key from source code or configuration | Critical | The JWT signing key is supplied via the `Jwt__SecretKey` environment variable. A code-level fallback exists for zero-config local development only. In production (Docker), the key is set via Docker Compose environment variables and never committed to source control. | Low in production; Medium for local dev fallback |
| I-04 | Sensitive data appears in log files | Medium | Serilog structured logging is configured to log request metadata (method, path, status code, elapsed time) but not request/response bodies containing sensitive data. Passwords are never logged. PIN values are never logged. | Low |
| I-05 | Unauthorized user accesses another user's transactions | High | JWT claims encode the user's identity and role. Endpoints enforce role-based authorization. The API Gateway validates the token before routing to backend services. | Low |

### D - Denial of Service

**Threat:** An attacker overwhelms the platform to prevent legitimate transactions from being processed.

| ID | Threat Description | Impact | Mitigation | Residual Risk |
|----|-------------------|--------|------------|---------------|
| D-01 | Attacker floods the login endpoint with credential stuffing attempts | High | Rate limiting middleware limits login requests to 5 per minute per IP. Failed authentication attempts are logged for monitoring. | Low |
| D-02 | Attacker floods API endpoints with high-volume requests | High | Rate limiting middleware enforces a general limit of 100 requests per minute per IP. Docker resource limits (CPU, memory) prevent a single container from consuming all host resources. | Medium - rate limits are per-instance |
| D-03 | Attacker generates high-value transactions to overwhelm risk and payment processing | Medium | The risk scoring engine applies higher scores to high-value transactions (amount > $5K adds +30, > $10K adds +50). Transactions above $50K are automatically rejected. This reduces processing load for extreme values. | Low |
| D-04 | SSE connection exhaustion | Medium | The InMemorySseHub manages client connections in memory. Connection limits should be enforced at the infrastructure level (load balancer, reverse proxy). Prometheus alerting monitors SSE connection counts. | Medium |

### E - Elevation of Privilege

**Threat:** A user with limited privileges gains administrative access or a service gains access to resources beyond its intended scope.

| ID | Threat Description | Impact | Mitigation | Residual Risk |
|----|-------------------|--------|------------|---------------|
| E-01 | Standard user escalates to admin role | Critical | Roles are embedded in the JWT token at issuance. Role claims are immutable within a token. The `[Authorize(Roles = "Admin")]` attribute is enforced server-side on every request. There is no dynamic role escalation mechanism. | Very Low |
| E-02 | Attacker exploits a vulnerability in a backend service to access the host | Critical | Services run in Docker containers with limited privileges. The `backend-net` network is isolated (`internal: true`). Container images are based on the official .NET runtime image with minimal attack surface. Trivy scans run in CI to detect vulnerable base images. | Low |
| E-03 | Frontend application bypasses API Gateway to reach backend services directly | High | Backend services are on the `backend-net` network with `internal: true`, meaning they have no route to the external internet. Frontend applications are on the separate `frontend-net` and can only reach the API Gateway. | Very Low |
| E-04 | Compromised service impersonates another service on the event bus | High | Services authenticate via the Docker network boundary. In production with Redis/Kafka, transport-level security and authentication credentials would be enforced. The AdaptiveEventBus selects the transport based on CPU load, and each backend implementation would enforce its own authentication. | Low with Redis/Kafka auth configured |

---

## Threat Summary

| STRIDE Category | Total Threats Identified | Critical | High | Medium | Low Residual |
|-----------------|--------------------------|----------|------|--------|--------------|
| Spoofing | 4 | 0 | 0 | 0 | 4 |
| Tampering | 4 | 0 | 0 | 0 | 4 |
| Repudiation | 3 | 0 | 0 | 0 | 3 |
| Information Disclosure | 5 | 0 | 0 | 1 | 4 |
| Denial of Service | 4 | 0 | 0 | 2 | 2 |
| Elevation of Privilege | 4 | 0 | 0 | 0 | 4 |
| **Total** | **24** | **0** | **0** | **3** | **21** |

---

## Residual Risks Requiring Acceptance

### I-03: JWT Signing Key Local Dev Fallback

**Risk:** The JWT signing key has a code-level fallback value (`"DefaultSecretKey_..."`) for zero-config local development. If this fallback is reached in production, tokens could be forged.

**Acceptance Criteria:** This fallback is acceptable only for local development. Production deployments MUST set `Jwt__SecretKey` via environment variable. CI/CD pipelines must validate that this variable is set in production configurations.

### D-02: Rate Limiting Per-Instance

**Risk:** Rate limiting is enforced per service instance. With horizontal scaling, an attacker could distribute requests across instances to bypass per-instance limits.

**Acceptance Criteria:** For the current deployment scale (single instances), this risk is acceptable. For production scaling, Redis-backed distributed rate limiting should be implemented.

### D-04: SSE Connection Exhaustion

**Risk:** The InMemorySseHub does not enforce a hard connection limit. A determined attacker could open many SSE connections to exhaust server resources.

**Acceptance Criteria:** For the current deployment, connection monitoring via Prometheus alerts is sufficient. For production, infrastructure-level connection limits (load balancer, nginx) should be configured.

---

## Review Schedule

This threat model is reviewed:
- Quarterly by the Security and Compliance Team
- After any significant architecture change
- Following a security incident
- Before major releases

---

## Change Log

| Date | Version | Author | Description |
|------|---------|--------|-------------|
| 2026-04-22 | 1.0 | Security & Compliance Team | Initial STRIDE threat model |
