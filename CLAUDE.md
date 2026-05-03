# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

---

## Build & Run Commands

### Backend (.NET 10)

```bash
cd backend

# Build all projects
dotnet build

# Run a specific service (ports: Gateway=5000, Transaction=5001, Risk=5002, Payment=5003, Compliance=5004, PIN=5005)
dotnet run --project src/FinancialPlatform.ApiGateway
dotnet run --project src/FinancialPlatform.ComplianceService

# Run all tests
dotnet test

# Run only unit tests
dotnet test tests/FinancialPlatform.UnitTests

# Run only integration tests
dotnet test tests/FinancialPlatform.IntegrationTests

# Run a single test by name
dotnet test --filter "FullyQualifiedName~AuditServiceTests"

# Run with verbose output
dotnet test -v normal

# Generate a new EF Core migration (example for ComplianceService)
dotnet ef migrations add MigrationName \
  --project src/FinancialPlatform.ComplianceService \
  --output-dir Data/Migrations

# Format check
dotnet format --verify-no-changes
```

### Frontend (React 19 + Vite)

Each app under `frontend/apps/` is independent. The `superapp` is the main new compliance-focused frontend; the others are the original dashboards.

```bash
# Install and run a specific app (replace <app-name>)
cd frontend/apps/<app-name> && npm install && npm run dev

# Run tests for an app
cd frontend/apps/<app-name> && npm test

# Build for production
npm run build

# Lint
npm run lint
```

### Docker (full stack)

```bash
cd infrastructure
docker-compose up --build          # All 18 services
docker-compose up sqlserver        # Just the database (for local .NET dev)
docker-compose down -v             # Stop + wipe data volumes
```

---

## Architecture

### Service Map

| Service | Port | Database | Description |
|---------|------|----------|-------------|
| ApiGateway | 5000 | — | JWT auth, SSE broadcast, routing |
| TransactionService | 5001 | `FinancialPlatform_Transactions` | Transaction CRUD |
| RiskService | 5002 | — | Rule-based risk scoring |
| PaymentService | 5003 | — | Payment authorization |
| ComplianceService | 5004 | `FinancialPlatform_Compliance` | SHA-256 audit hash chain |
| PinEncryptionService | 5005 | — | ISO 9564 PIN blocks, HSM simulation |

### Event Pipeline

Non-card: `TransactionCreated → RiskEvaluated → PaymentAuthorized → AuditLogged`

Card: `TransactionCreated → RiskEvaluated + PinVerified → PaymentAuthorized → AuditLogged`

All pipeline events are logged by ComplianceService and broadcast via SSE to frontends.

### Adaptive Event Bus (`FinancialPlatform.EventInfrastructure`)

The `AdaptiveEventBus` switches messaging backends based on CPU load or `EventBus__DefaultBackend` config:
- **InMemory** — default for local dev; events stay in-process
- **Redis Streams** — default in Docker; uses XADD/XREADGROUP with consumer groups
- **RabbitMQ** — fanout exchange with durable per-service queues
- **Kafka** — topics per event type with consumer groups

In local dev without Docker, cross-service events don't flow unless you set `EventBus__DefaultBackend=Redis` and have Redis running.

### Shared Library (`FinancialPlatform.Shared`)

All services reference this project for:
- `Models/` — `Transaction`, `AuditLog`, `RiskAssessment`, `User`
- `Events/` — `TransactionCreatedEvent`, `RiskEvaluatedEvent`, `PaymentAuthorizedEvent`, `PinVerifiedEvent`, `AuditLoggedEvent`
- `DTOs/` — Request/response contracts
- `Enums/` — `TransactionStatus`, `UserRole`, `EventBusBackend`
- `Interfaces/` — `IEventBus`, `ISseHub`

New enums, models, events, and DTOs for OJK compliance go here.

### ComplianceService Internals

- `Data/ComplianceDbContext.cs` — EF Core DbContext; add new `DbSet<T>` here for new tables
- `Data/Migrations/` — EF Core migrations; generate with `dotnet ef migrations add`
- `Services/AuditService.cs` — SHA-256 hash chain logic; each entry hashes `Payload + PreviousHash`
- `Services/EventSubscriber.cs` — Background `IHostedService` that subscribes to all events via `IEventBus`; uses `IServiceScopeFactory` to resolve scoped services like `AuditService`
- `Controllers/AuditController.cs` — `[Route("api/audit")]`
- `Metrics.cs` — Prometheus gauge for event bus backend; exposed at `/metrics`

### Database Migrations

EF Core migrations are applied automatically at startup via `MigrateAsync()` in `Program.cs`. The `IsRelational()` guard skips this for in-memory test databases. No manual `dotnet ef database update` needed.

### Frontend Apps

| App | Port | Live data |
|-----|------|-----------|
| `transaction-ui` | 3001 | Yes (API Gateway + SSE) |
| `admin-dashboard` | 3002 | Mock only |
| `risk-dashboard` | 3003 | Mock only |
| `audit-dashboard` | 3004 | Mock only |
| `card-operations` | 3005 | Yes (PinEncryptionService) |
| `superapp` | — | New; React Router + TanStack Query |

`superapp` uses React Router v7, TanStack Query v5, Zustand v5, Recharts v3, Tailwind CSS v4, and Vitest for tests.

### Authentication

JWT tokens are issued by ApiGateway (`POST /api/auth/login`). Roles: `Guest`, `User`, `Admin`, `Auditor`. Default credentials: `admin` / `admin123`. Token TTL: 1 hour.

### Risk Scoring

Scores 0–100: HIGH ≥ 80, MEDIUM ≥ 50, LOW < 50. Points: amount > 5k (+30), amount > 10k (+20 extra), velocity > 5 tx/min (+25), odd hours 22:00–06:00 (+15).

### Payment Authorization Rules (first match wins)

PIN failed → reject | amount > 50k → reject | risk ≥ 80 → reject | amount > 5k AND risk ≥ 50 → reject | else → approve

---

## Key Conventions

- **Scoped vs Singleton:** `AuditService` (and any service touching `DbContext`) must be Scoped. Background services use `IServiceScopeFactory` to avoid captive dependency issues.
- **No new microservices:** All new OJK compliance features go into the existing `ComplianceService` (and lightweight additions to `RiskService`).
- **Migration output dir:** Always use `--output-dir Data/Migrations` when generating migrations.
- **Tests:** Unit tests use Moq; integration tests spin up `WebApplicationFactory<Program>` with in-memory databases.
- **Windows Smart App Control:** If integration tests fail with "Application Control policy blocked this file", disable Smart App Control in Windows Security.
