# System Design Document

## Adaptive Real-Time Financial Transaction Processing Platform

### C# Microservices + React Microfrontends + SQL Server + Adaptive Event Infrastructure + Observability + Compliance

---

# 1. System Overview

This system is a distributed, real-time financial transaction processing platform that simulates enterprise-grade fintech infrastructure. It processes financial transactions through a multi-stage pipeline - creation, risk scoring (with optional PIN verification for card transactions), payment authorization, and audit logging - connected by an adaptive event bus.

The architecture consists of six independently deployable microservices, five React microfrontends, and supporting infrastructure (SQL Server, Redis, RabbitMQ, Kafka, Prometheus, Grafana). Each service communicates through an event-driven architecture using a pluggable event bus that adapts its backend based on system load.

---

# 2. Design Goals

| Goal | Implementation |
|------|---------------|
| **Stateless services** | No service stores critical runtime state permanently; state is reconstructed from events or stored in SQL Server |
| **Event-driven** | Every business action produces an event: `TransactionCreated`, `RiskEvaluated`, `PinVerified` (card transactions), `PaymentAuthorized`, `AuditLogged` |
| **Adaptive infrastructure** | Event bus dynamically selects messaging backend based on CPU load (InMemory → Redis → RabbitMQ → Kafka) |
| **Financial precision** | All monetary calculations use `decimal` (128-bit) - never `float` or `double` - to prevent rounding errors |
| **Observability first** | Every request and event is logged with Serilog, measured with Prometheus, and visualized in Grafana |
| **Immutability** | Audit logs are linked with SHA-256 hashes; tampering breaks the chain and is detectable |
| **Card payment support** | ISO 8583 messaging, ISO 9564 PIN block encryption, and simulated HSM for card transaction processing |

---

# 3. Architecture

```
┌──────────────────────────────────────────────────────────────────────┐
│  FRONTEND (React + Vite + Tailwind CSS v4)                          │
│  Transaction-UI :3001  Admin :3002  Risk :3003  Audit :3004         │
│  Card Operations :3005                                               │
└────────────────────────────┬─────────────────────────────────────────┘
                             │ HTTP requests / SSE stream
┌────────────────────────────▼─────────────────────────────────────────┐
│                     API GATEWAY (:5000)                               │
│  JWT Authentication · SSE Streaming (/api/sse/stream)                │
│  Health Checks (/api/gateway/health) · Request Logging               │
└────────────────────────────┬─────────────────────────────────────────┘
                             │ Events published to IEventBus
         ┌───────────────────┼───────────────────────┬────────────────┐
         │                   │                       │                │
┌────────▼─────────┐ ┌───────▼───────┐ ┌────────────▼─────────┐ ┌────▼──────────────┐
│ TRANSACTION SVC  │ │  RISK SVC     │ │  PAYMENT SVC         │ │ PIN ENCRYPTION    │
│    (:5001)       │ │  (:5002)      │ │   (:5003)            │ │ SVC (:5005)       │
│                  │ │               │ │                      │ │                   │
│ EF Core + SQL    │ │ In-memory     │ │ In-memory            │ │ ISO 8583 msgs     │
│ Create/Query     │ │ velocity      │ │ auth rules           │ │ HSM key mgmt      │
│ Transaction      │ │ tracking      │ │ Amount limits        │ │ PIN block encrypt  │
│ Validation       │ │ Score calc    │ │ Risk + PIN check     │ │ Event subscriber  │
└────────┬─────────┘ └───────┬───────┘ └────────────┬─────────┘ └────┬──────────────┘
         │                   │                       │                │
         └───────────────────┼───────────────────────┘────────────────┘
                             │ All events logged
                ┌────────────▼────────────┐
                │   COMPLIANCE SVC (:5004) │
                │   EF Core + SQL Server   │
                │   SHA-256 hash chain     │
                │   Prometheus /metrics    │
                └─────────────────────────┘
```

---

# 4. Service Specifications

## 4.1 API Gateway (port 5000)

The entry point for all client requests. Does not connect to a database.

| Endpoint | Method | Auth | Description |
|----------|--------|------|-------------|
| `/api/auth/login` | POST | No | Authenticate user, returns JWT token (1h expiry) |
| `/api/auth/register` | POST | No | Register new user with role validation |
| `/api/gateway/health` | GET | No | Returns health status of all services |
| `/api/gateway/status` | GET | Yes (JWT) | SSE connection count + event bus backend |
| `/api/sse/stream` | GET | No | Server-Sent Events stream (text/event-stream) |

**Authentication flow:**
1. Client sends `POST /api/auth/login` with `{ username, password }`
2. Server validates credentials against in-memory user list
3. Returns JWT token signed with HS256 (1-hour expiry)
4. Client includes `Authorization: Bearer <token>` on protected endpoints

**Roles:** Guest, User, Admin, Auditor - validated via `Enum.TryParse<UserRole>()`.

**Default user:** `admin` / `admin123` (Admin role).

**SSE protocol:**
- Client connects to `/api/sse/stream`
- Server assigns a client ID and adds the response to the hub
- Events are sent as `data: {...}\n\n`
- Client disconnects are detected via `CancellationToken`

## 4.2 Transaction Service (port 5001)

Manages financial transactions. Persists to SQL Server via EF Core.

| Endpoint | Method | Description |
|----------|--------|-------------|
| `/api/transactions` | POST | Create transaction (validates amount > 0) |
| `/api/transactions` | GET | List all transactions (newest first) |
| `/api/transactions/{id}` | GET | Get transaction by ID |
| `/api/transactions/user/{userId}` | GET | Get transactions for a specific user |

**Database tables:**

```sql
CREATE TABLE Transactions (
    Id           NVARCHAR(50)  PRIMARY KEY,
    UserId       NVARCHAR(50)  NOT NULL,
    Amount       DECIMAL(18,2) NOT NULL,
    Currency     NVARCHAR(3)   NOT NULL DEFAULT 'USD',
    Timestamp    DATETIME2     NOT NULL,
    Status       NVARCHAR(50)  NOT NULL DEFAULT 'Pending',
    Description  NVARCHAR(500) NULL,
    Counterparty NVARCHAR(200) NULL
);
CREATE INDEX IX_Transactions_UserId ON Transactions(UserId);
CREATE INDEX IX_Transactions_Timestamp ON Transactions(Timestamp);

CREATE TABLE Users (
    Id           NVARCHAR(50)  PRIMARY KEY,
    Username     NVARCHAR(100) NOT NULL UNIQUE,
    PasswordHash NVARCHAR(255) NOT NULL
);
CREATE UNIQUE INDEX IX_Users_Username ON Users(Username);
```

**Validation rules:**
- Amount must be > 0 (throws `ArgumentException`)
- Currency defaults to "USD"
- Status defaults to "Pending"
- ID and timestamp are auto-generated

**Event published:** `TransactionCreatedEvent(TransactionId, UserId, Amount, Currency, Timestamp)`

## 4.3 Risk Service (port 5002)

Stateless service that evaluates transaction risk. No database.

| Endpoint | Method | Description |
|----------|--------|-------------|
| `/api/risk/health` | GET | Service health check |
| `/api/risk/evaluate` | POST | Manually trigger evaluation |

**Risk scoring algorithm (score 0–100):**

| Factor | Condition | Points Added |
|--------|-----------|-------------|
| High amount | Amount > 5,000 | +30 |
| Very high amount | Amount > 10,000 | +20 (total: 50) |
| High velocity | > 5 transactions/minute per user | +25 |
| Odd hours | Between 10 PM and 6 AM | +15 |

**Risk levels:**
- HIGH: score >= 80
- MEDIUM: score >= 50
- LOW: score < 50

**Flags detected:** `HIGH_AMOUNT`, `HIGH_VELOCITY`, `ODD_HOUR`

**Velocity tracking:** Maintains a `Dictionary<string, List<DateTime>>` with a 1-minute sliding window per user. Entries older than 1 minute are pruned on each evaluation.

**Event published:** `RiskEvaluatedEvent(TransactionId, RiskScore, RiskLevel, Flags, EvaluatedAt)`

## 4.4 Payment Service (port 5003)

Stateless service that authorizes payments based on rules.

| Endpoint | Method | Description |
|----------|--------|-------------|
| `/api/payment/health` | GET | Service health check |
| `/api/payment/status/{transactionId}` | GET | Check tracked status |
| `/api/payment/authorize` | POST | Manual authorization check |

**Authorization rules (evaluated in order):**

| Condition | Result |
|-----------|--------|
| Amount > 50,000 | Rejected - "Amount exceeds daily limit" |
| Risk score >= 80 | Rejected - "Risk score too high" |
| Amount > 5,000 AND risk score >= 50 | Rejected - "High amount with elevated risk" |
| Otherwise | Approved - "Authorized" |

**Pending transaction tracking:** Amounts are stored in a `Dictionary<string, decimal>` keyed by transaction ID. This is populated on `TransactionCreatedEvent` and consumed on `RiskEvaluatedEvent`. Thread-safe via `lock`.

**Event published:** `PaymentAuthorizedEvent(TransactionId, Authorized, Reason, AuthorizedAt)`

## 4.5 Compliance Service (port 5004)

Persists every system event as an immutable audit log with SHA-256 hash chaining.

| Endpoint | Method | Description |
|----------|--------|-------------|
| `/api/audit` | GET | Paginated audit logs (`?page=1&pageSize=20`, max 100) |
| `/api/audit/{id}` | GET | Get specific audit log |
| `/api/audit/verify` | GET | Verify hash chain integrity |
| `/api/audit/stats` | GET | Statistics grouped by event type |
| `/metrics` | GET | Prometheus metrics endpoint |

**Database table:**

```sql
CREATE TABLE AuditLogs (
    Id           NVARCHAR(50)   PRIMARY KEY,
    EventType    NVARCHAR(100)  NOT NULL,
    Payload      NVARCHAR(MAX)  NOT NULL,
    Hash         NVARCHAR(255)  NOT NULL,
    PreviousHash NVARCHAR(255)  NULL,
    CreatedAt    DATETIME2      NOT NULL
);
CREATE INDEX IX_AuditLogs_EventType ON AuditLogs(EventType);
CREATE INDEX IX_AuditLogs_CreatedAt ON AuditLogs(CreatedAt);
```

**Hash chain algorithm:**

```
Hash(1) = SHA256(Payload(1) + "")
Hash(n) = SHA256(Payload(n) + Hash(n-1))
```

The `verify` endpoint iterates all logs ordered by `CreatedAt` and recomputes each hash, comparing it to the stored value. If any hash does not match, the chain is broken.

**Events logged:** `TransactionCreatedEvent`, `RiskEvaluatedEvent`, `PaymentAuthorizedEvent`, `PinVerifiedEvent`

## 4.6 PIN Encryption Service (port 5005)

Simulates a Hardware Security Module (HSM) and processes card transactions through ISO 8583 messaging. No database (all state in-memory).

| Endpoint | Method | Description |
|----------|--------|-------------|
| `/api/hsm/health` | GET | Service health with key count |
| `/api/hsm/keys` | GET | List stored key IDs |
| `/api/hsm/keys/generate` | POST | Generate a new key (ZMK/ZPK/PVK/CVK/TAK) |
| `/api/hsm/pin/encrypt` | POST | Encrypt a PIN under a ZPK (ISO 9564 Format 0) |
| `/api/hsm/pin/decrypt` | POST | Decrypt a PIN block |
| `/api/hsm/pin/translate` | POST | Translate PIN from one ZPK to another |
| `/api/hsm/pin/verify` | POST | Verify PIN against expected value |
| `/api/iso8583/fields` | GET | List supported ISO 8583 field definitions |
| `/api/iso8583/parse` | POST | Parse a raw ISO 8583 message |
| `/api/iso8583/build` | POST | Build a raw ISO 8583 message |
| `/api/iso8583/authorize` | POST | Full card authorization (0100 request with PIN) |

**HSM simulation:**
- Keys stored encrypted under a Local Master Key (LMK) using AES-256-ECB
- LMK loaded from `Hsm:LmkHex` config, or a development default
- Seeds a `default-zpk` with well-known test key for development
- All PIN operations use ISO 9564-1 Format 0 (ANSI X9.8): PIN block XOR PAN block, then 3DES-ECB under ZPK

**ISO 8583 implementation:**
- ASCII presentation format: `[MTI:4][Bitmap:16 hex][Fields...]`
- Supports 17 data elements (fields 2, 3, 4, 7, 11-14, 22, 35, 37-39, 41, 42, 49, 52, 55)
- Three field types: FIXED (exact length), LLVAR (2-digit length prefix), LLLVAR (3-digit prefix)
- Card authorization builds 0100 request with encrypted PIN in field 52

**Event subscriber:**
- Subscribes to `TransactionCreatedEvent`
- Skips non-card transactions (no PAN/PIN block)
- For card transactions: decrypts PIN block via HSM, validates format, publishes `PinVerifiedEvent`

**Event published:** `PinVerifiedEvent(TransactionId, Pan, Verified, Message, VerifiedAt)`

**Card transaction flow impact:**
- `PinVerifiedEvent` is consumed by PaymentService: if `Verified=false`, payment is rejected
- `PinVerifiedEvent` is audited by ComplianceService alongside all other events
- Non-card transactions are unaffected (no PinVerifiedEvent published)

---

# 5. Event Infrastructure

## 5.1 IEventBus Interface

```csharp
public interface IEventBus
{
    Task PublishAsync<T>(T evt);
    Task SubscribeAsync<T>(Func<T, Task> handler);
    Task StartAsync(CancellationToken ct = default);
    Task StopAsync(CancellationToken ct = default);
}
```

## 5.2 Adaptive Event Bus

Monitors CPU usage every 30 seconds and auto-switches backends:

| CPU Range | Backend | Status |
|-----------|---------|--------|
| < 25% | InMemory | Fully implemented |
| 25–50% | Redis Streams | Stub (logs intent) |
| 50–75% | RabbitMQ | Stub (logs intent) |
| >= 75% | Kafka | Stub (logs intent) |

The switch fires a `BackendChanged` event that other components can subscribe to.

## 5.3 InMemory Event Bus

Uses `Dictionary<Type, List<Func<object, Task>>>` to store handlers. Events are delivered synchronously (all handlers run in sequence). This is suitable for single-process development.

## 5.4 SSE Hub

`InMemorySseHub` maintains a `ConcurrentDictionary<string, HttpResponse>` of connected clients. `BroadcastAsync` serializes events as JSON and writes `data: {...}\n\n` to each client's response stream.

---

# 6. Event Flow Diagram

**Non-card transactions:**
```
Client                  Transaction       Risk           Payment        Compliance
  │                     Service           Service        Service        Service
  │                         │                 │              │              │
  ├─POST /transactions─────►│                 │              │              │
  │                         ├─validate───────│              │              │
  │                         ├─save to DB─────│              │              │
  │                         ├─publish────────►│              │              │
  │                         │ Transaction     │              │              │
  │                         │ CreatedEvent    │              │              │
  │                         │                 ├─calc score──│              │
  │                         │                 ├─publish─────►│              │
  │                         │                 │ Risk         │              │
  │                         │                 │ Evaluated    │              │
  │                         │                 │ Event        │              │
  │                         │                 │              ├─authorize───│
  │                         │                 │              ├─publish─────►│
  │                         │                 │              │ Payment      │
  │                         │                 │              │ Authorized   │
  │                         │                 │              │ Event        │
  │                         │                 │              │              ├─hash chain
  │                         │                 │              │              ├─save to DB
  │                         │                 │              │              ├─broadcast SSE
  │◄───────────────────SSE stream (all events)─────────────────────────────┤
```

**Card transactions (with PAN + PIN block):**
```
Client       Transaction   Risk         PIN Encryption   Payment      Compliance
  │              Service    Service      Service          Service      Service
  │                │           │            │                │            │
  ├─POST /tx──────►│           │            │                │            │
  │ (with PAN/PIN) ├─save──────│            │                │            │
  │                ├─publish───►│            │                │            │
  │                │ TxCreated ├─publish────►│                │            │
  │                │           │            ├─decrypt PIN────│            │
  │                │           │            ├─publish────────►│            │
  │                │           │            │ PinVerified     │            │
  │                │           ├─publish────│────────────────►│            │
  │                │           │ RiskEval   ├─check both─────│            │
  │                │           │            │                ├─publish────►│
  │                │           │            │                │ PaymentAuth │
  │                │           │            │                │             ├─audit all
  │◄────────────────────────────SSE stream (all events including PinVerified)──────┤
```

---

# 7. Frontend Architecture

Five independent Vite + React + TypeScript applications, each on a different port.

| App | Port | Features |
|-----|------|----------|
| Transaction UI | 3001 | Create transactions, view table, KPI cards (total, volume, average, pending) |
| Admin Dashboard | 3002 | Service health monitoring, metrics panel, Run Health Check / Clear Cache controls |
| Risk Dashboard | 3003 | Semi-circular risk gauge, risk distribution pie chart, event list with badges |
| Audit Dashboard | 3004 | Hash chain timeline, filter by event type, search by hash/type/payload, chain visualization |
| Card Operations | 3005 | HSM key management, PIN encrypt/decrypt/verify, ISO 8583 message tools, card authorization |

**Design system:** High-contrast dark theme (#0A0A0A background, #FFFFFF text, WCAG AAA contrast ratios).

**State management:** Zustand for client state, @tanstack/react-query for server state.

**Proxies:** Transaction UI proxies `/api` to the API Gateway (localhost:5000) via Vite config. Card Operations proxies `/api` to the PIN Encryption Service (localhost:5005).

---

# 8. Database Strategy

- **SQL Server 2022** for persistent storage (Transactions, Users, AuditLogs)
- **EF Core Code-First** with automatic migrations on startup
- **InMemory provider** used in tests (no SQL Server needed for test runs)
- **JSON seed data** in `database/seeds/seed_data.json` for initial users

---

# 9. Testing Strategy

## 9.1 Backend Unit Tests (130 tests)

Test framework: **xUnit** with **Moq** for mocking.

| Test Class | Tests | What's Tested |
|------------|-------|--------------|
| AdaptiveEventBusTests | 8 | Bus lifecycle, publish/subscribe, backend switching |
| InMemorySseHubTests | 7 | Client management, broadcast, connection count |
| TransactionServiceTests | 13 | CRUD, validation, event publishing |
| AuditServiceIntegrationTests | 13 | Hash chain creation, SHA-256 correctness |
| RiskEvaluationServiceTests | 8 | Score calculation, velocity, odd hours |
| PaymentGatewayTests | 9 | Authorization rules |
| PinBlockServiceTests | 9 | ISO 9564 PIN block encrypt/decrypt, Format 0 layout, PAN block, KCV, translation |
| JwtServiceTests | 6 | Token generation, claims |
| EventSerializer / EventTypeResolver | 10 | JSON round-trip, type resolution for all 5 event types |

Uses EF Core InMemory provider with `Guid.NewGuid()` database names for isolation.

## 9.2 Backend Integration Tests (29 tests)

Test framework: **xUnit** + **WebApplicationFactory** (in-process HTTP server).

| Test Class | Tests | What's Tested |
|------------|-------|--------------|
| ApiGatewayIntegrationTests | 9 | Login/register/health/auth via real HTTP |
| TransactionServiceIntegrationTests | 7 | Full CRUD via HTTP with InMemory DB |
| EventFlowTests | 3 | End-to-end pipeline: create → risk → payment → audit |
| PinEncryptionServiceTests | 10 | HSM health, key management, PIN encrypt/decrypt/verify, ISO 8583 parse/build/authorize |

## 9.3 Frontend Tests (123 tests)

Test framework: **Vitest** + **@testing-library/react** + **jsdom**.

| App | Tests | What's Tested |
|-----|-------|--------------|
| transaction-ui | 43 | Store, API client, KPI cards, table, form |
| admin-dashboard | 16 | Header, services, health check, metrics, timers |
| risk-dashboard | 13 | Header, score, alerts, badges, gauge, chart |
| audit-dashboard | 21 | Header, stats, filters, search, chain, timeline |
| card-operations | 30 | API client, HSM key management, PIN operations, ISO 8583 tools |

**Total: 282 tests across all layers.**

---

# 10. Observability

| Component | Technology | Purpose |
|-----------|-----------|---------|
| Structured logging | Serilog | JSON-formatted logs to console + rolling files |
| Request logging | Custom middleware | Method, path, status code, duration (ms) per request |
| Application metrics | prometheus-net | Counter/histogram/gauge metrics at /metrics |
| Metrics collection | Prometheus | Scrapes /metrics every 15 seconds |
| Dashboards | Grafana | Pre-provisioned with Prometheus data source |

---

# 11. Security

- **JWT authentication** - HS256 signed tokens, 1-hour expiry
- **Role-based access** - Guest, User, Admin, Auditor roles enforced via `[Authorize]`
- **Immutable audit logs** - SHA-256 hash chain prevents undetected tampering
- **Request logging** - Full request/response audit trail in logs
- **PIN encryption** - ISO 9564 Format 0 PIN blocks encrypted under 3DES with Zone PIN Keys
- **HSM key management** - Keys stored encrypted under AES-256 Local Master Key; dev LMK is never for production
- **PIN never echoed** - `TransactionResponse` DTO omits PinBlock field to prevent PIN exposure in API responses

---

# 12. Deployment

## Docker Compose (Development)

```bash
cd infrastructure
docker-compose up --build    # Start everything
docker-compose down -v       # Stop and clean
```

Containers: SQL Server, Redis, RabbitMQ, Zookeeper, Kafka, 6 .NET services, 5 React frontends, Prometheus, Grafana.

## Visual Studio

Open `backend/FinancialPlatform.slnx`, configure all 6 service projects as startup projects, press F5.

## Production Considerations

- Replace in-memory user store with a real database
- Use BCrypt for password hashing (currently plain text for simplicity)
- Configure Redis/RabbitMQ/Kafka event bus backends for cross-service communication
- Add HTTPS/TLS termination
- Use managed SQL Server (Azure SQL, AWS RDS)
- Add health check endpoints for Kubernetes liveness/readiness probes
- Implement rate limiting at the API Gateway
