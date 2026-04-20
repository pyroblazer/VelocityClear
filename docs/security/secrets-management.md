# Secrets Management

**Platform:** VelocityClear Adaptive Real-Time Financial Transaction Platform
**Last Reviewed:** 2026-04-22
**Owner:** Security & Compliance Team

---

## Overview

This document describes how sensitive configuration values (secrets) are managed across the VelocityClear platform. It covers secret types, storage mechanisms, rotation procedures, and the separation between development and production environments.

---

## Secret Inventory

### 1. JWT Signing Key

| Property | Value |
|----------|-------|
| **Environment Variable** | `Jwt__SecretKey` |
| **Used By** | API Gateway (JwtService) |
| **Purpose** | Signs and verifies JWT authentication tokens using HMAC-SHA256 |
| **Minimum Length** | 32 characters (256 bits) |
| **Format** | Arbitrary string |
| **Storage** | Docker Compose environment variable; `.env` file (gitignored) |

**Implementation Details:**

The JWT signing key is read from configuration in `JwtService.cs`. In production, the key is supplied exclusively through the `Jwt__SecretKey` environment variable set in Docker Compose.

```
Jwt__SecretKey=<random-256-bit-string>
```

**Local Development Fallback:**

A code-level fallback (`"DefaultSecretKey_..."`) exists for zero-config local development. This fallback MUST NEVER be used in production. The presence of the fallback in code is documented as a known gap and flagged for future removal.

**Rotation Procedure:**

1. Generate a new 256-bit random key: `openssl rand -base64 32`
2. Update the `Jwt__SecretKey` environment variable in Docker Compose or the orchestration platform
3. Restart the API Gateway service
4. All existing JWT tokens become invalid upon restart; users must re-authenticate
5. Verify that new tokens are issued and accepted across all services
6. Schedule rotation at least every 90 days

---

### 2. SQL Server SA Password

| Property | Value |
|----------|-------|
| **Environment Variable** | `SA_PASSWORD` (Docker), `ConnectionStrings__DefaultConnection` (services) |
| **Used By** | SQL Server container, TransactionService, ComplianceService |
| **Purpose** | Database authentication for schema ownership and data access |
| **Format** | SQL Server password policy compliant (upper, lower, digit, special, min 8 chars) |
| **Storage** | Docker Compose environment variable; `.env` file (gitignored) |

**Implementation Details:**

The SA password is set when the SQL Server Docker container starts. Backend services reference the password within the `ConnectionStrings__DefaultConnection` environment variable, which is constructed in Docker Compose using the SA password.

```
SA_PASSWORD=<strong-password>
ConnectionStrings__DefaultConnection=Server=sqlserver;Database=<dbname>;User Id=sa;Password=<strong-password>;TrustServerCertificate=True;
```

**Rotation Procedure:**

1. Generate a new strong password meeting SQL Server complexity requirements
2. Connect to the running SQL Server container: `docker exec -it sqlserver /opt/mssql-tools/bin/sqlcmd -S localhost -U sa -P <old-password>`
3. Change the SA password: `ALTER LOGIN sa WITH PASSWORD = '<new-password>';`
4. Update the `SA_PASSWORD` and `ConnectionStrings__DefaultConnection` environment variables in Docker Compose
5. Restart all backend services to pick up the new connection string
6. Verify that TransactionService and ComplianceService can connect and perform operations
7. Schedule rotation at least every 180 days

---

### 3. HSM Local Master Key (LMK)

| Property | Value |
|----------|-------|
| **Environment Variable** | `Hsm__LmkHex` |
| **Used By** | PIN Encryption Service (HsmSimulator) |
| **Purpose** | Master key for deriving PIN encryption keys; used in AES-256-CBC operations and 3DES PIN block processing |
| **Minimum Length** | 64 hex characters (256 bits) |
| **Format** | Hexadecimal string |
| **Storage** | Docker Compose environment variable; `.env` file (gitignored) |

**Implementation Details:**

The LMK is the root of trust for the simulated HSM. All PIN encryption, decryption, translation, and verification operations derive their working keys from the LMK. The key is loaded into memory on service startup and never persisted to disk.

```
Hsm__LmkHex=<64-character-hex-string>
```

**Local Development Fallback:**

A well-known development LMK value is used as a fallback when `Hsm__LmkHex` is not set. This is documented and MUST NOT be used in production. Any deployment handling real card data must set a unique, randomly generated LMK.

**Rotation Procedure:**

1. Generate a new 256-bit LMK: `openssl rand -hex 32`
2. **Critical:** Before rotation, ensure no encrypted PIN blocks are in transit or pending processing. The rotation must be performed during a maintenance window.
3. Update the `Hsm__LmkHex` environment variable
4. Restart the PIN Encryption Service
5. Note: Existing encrypted PIN blocks encrypted with the old LMK cannot be decrypted with the new LMK. Plan rotation when no pending PIN operations exist.
6. Verify PIN encryption/decryption round-trip: encrypt a test PIN, decrypt it, and confirm the result matches
7. Schedule rotation at least annually, or immediately upon any suspected compromise

---

### 4. Redis Connection (When Used)

| Property | Value |
|----------|-------|
| **Environment Variable** | `EventBus__RedisUrl` |
| **Used By** | All backend services (via RedisEventBus) |
| **Purpose** | Event bus transport for cross-service event propagation |
| **Format** | Redis URL (e.g., `redis:6379`) |
| **Storage** | Docker Compose environment variable |

**Implementation Details:**

When Redis is selected as the event bus backend (`EventBus__DefaultBackend=Redis`), all services connect using this URL. Redis authentication can be enabled by appending the password to the URL or setting the `EventBus__RedisPassword` environment variable.

```
EventBus__RedisUrl=redis:6379
EventBus__RedisPassword=<optional-password>
```

**Rotation Procedure:**

1. Update the Redis `requirepass` configuration
2. Update `EventBus__RedisPassword` in Docker Compose for all backend services
3. Restart Redis and all backend services
4. Verify that events propagate correctly across services
5. Schedule rotation at least every 90 days

---

## Secret Storage Architecture

### Docker Compose (Current)

```
infrastructure/
  .env                  # Secrets (gitignored, never committed)
  .env.example          # Template with placeholder values (committed)
  docker-compose.yml    # References ${VARIABLE} from .env
```

All secrets are injected into containers via the Docker Compose `environment:` block, which reads values from the `.env` file. The `.env` file is listed in `.gitignore` and MUST NEVER be committed to version control.

The `.env.example` file provides a template with placeholder values for each required secret. Developers copy `.env.example` to `.env` and replace placeholders with actual values.

### Production Recommendations

For production deployments, the following secret management solutions are recommended:

1. **HashiCorp Vault** - Centralized secret storage with dynamic secret generation, automatic rotation, and audit logging
2. **Azure Key Vault** - If deployed on Azure, integrates with Managed Identities for password-less service authentication
3. **AWS Secrets Manager** - If deployed on AWS, integrates with IAM roles for secure secret retrieval
4. **Kubernetes Secrets** - If orchestrated with Kubernetes, use sealed secrets or external secrets operator for gitOps workflows

---

## Secret Handling Rules

### Rules for Developers

1. **Never commit secrets to source control.** All secrets must be provided via environment variables or secret management tools. The `.gitignore` file must include `.env`, `*.key`, `*.pem`, and any other secret-bearing files.

2. **Never log secrets.** Serilog configuration must exclude any configuration keys containing passwords, keys, or tokens. Review log output to confirm no secrets are emitted.

3. **Never hard-code secrets in source code.** The existing code-level fallbacks (JWT signing key, HSM LMK) are documented exceptions for local development only and must not be used in any shared or production environment.

4. **Use the `.env.example` template.** When adding a new secret, update `.env.example` with a clearly fake placeholder value and document the expected format and minimum requirements in this document.

5. **Rotate secrets on schedule.** Follow the rotation procedures defined for each secret type. Initiate emergency rotation immediately upon any suspected compromise.

### Rules for CI/CD

1. **GitHub Actions secrets** are used for CI/CD pipeline secrets (e.g., deployment credentials, API keys). These are configured in the repository settings and accessed via `${{ secrets.NAME }}` syntax.

2. **Trivy scans** must pass with zero CRITICAL and HIGH findings before any deployment. Vulnerabilities in secret handling libraries (e.g., `System.Security.Cryptography`) are treated as blockers.

3. **No secrets in build artifacts.** Docker images must not contain `.env` files or secret values. All secrets are injected at runtime via environment variables.

---

## Secret Rotation Schedule

| Secret | Rotation Frequency | Last Rotated | Next Due |
|--------|--------------------|--------------|----------|
| JWT Signing Key | Every 90 days | Initial deployment | 2026-07-21 |
| SQL Server SA Password | Every 180 days | Initial deployment | 2026-10-19 |
| HSM LMK | Every 365 days (or upon compromise) | Initial deployment | 2027-04-22 |
| Redis Password | Every 90 days | Initial deployment | 2026-07-21 |

---

## Emergency Secret Rotation

In the event of a suspected or confirmed secret compromise:

1. **Immediately** rotate the compromised secret following the procedure above
2. **Within 1 hour** notify the Security and Compliance Team
3. **Within 4 hours** complete a preliminary incident assessment
4. **Within 24 hours** complete a full incident report including impact analysis
5. **Within 48 hours** review and update this document with lessons learned

For the JWT signing key specifically, rotation invalidates all active user sessions. Users must re-authenticate. Communicate this impact to stakeholders before rotation if the compromise is not actively being exploited.

For the HSM LMK specifically, rotation invalidates any encrypted PIN blocks in transit. Coordinate rotation during a maintenance window when no card transactions are being processed.

---

## Audit and Compliance

- All secret access and rotation events should be logged to the ComplianceService audit trail
- This secrets management document is reviewed quarterly and after any security incident
- ISO 27001 Annex A control A.9.2.4 (Management of secret authentication information) requires documented procedures for secret lifecycle management, which this document satisfies

---

## Change Log

| Date | Version | Author | Description |
|------|---------|--------|-------------|
| 2026-04-22 | 1.0 | Security & Compliance Team | Initial secrets management documentation |
