# Adaptive Real-Time Financial Transaction Processing Platform (VelocityClear)

A distributed, real-time financial transaction processing platform built with **.NET 10 microservices** and **React microfrontends**. The system processes financial transactions through a multi-stage pipeline: creation, risk scoring (with optional PIN verification for card transactions), payment authorization, and immutable audit logging - all connected by an adaptive event bus that switches backends based on system load.

---

## Table of Contents

1. [Architecture Overview](#architecture-overview)
2. [Features](#features)
3. [Tech Stack](#tech-stack)
4. [Prerequisites](#prerequisites)
5. [Quick Start (Docker)](#quick-start-docker)
6. [Running Locally without Docker](#running-locally-without-docker)
7. [Using the Platform - Step by Step](#using-the-platform--step-by-step)
8. [API Reference](#api-reference)
9. [Event Flow and Business Rules](#event-flow-and-business-rules)
10. [Frontend Applications](#frontend-applications)
11. [Swagger UI (Interactive API Docs)](#swagger-ui-interactive-api-docs)
12. [Postman Collection](#postman-collection)
13. [Event Bus Backends](#event-bus-backends)
14. [Observability (Prometheus + Grafana)](#observability-prometheus--grafana)
15. [Database](#database)
16. [Testing](#testing)
17. [Running in Visual Studio](#running-in-visual-studio)
18. [Project Structure](#project-structure)
19. [Environment Variables](#environment-variables)
20. [Troubleshooting](#troubleshooting)

---

## Architecture Overview

```
                         ┌──────────────────────────────────────────────┐
                         │         FRONTEND (React)                     │
                         │  Transaction UI  Admin Dashboard             │
                         │  Risk Dashboard  Audit Dashboard             │
                         │  Card Operations                              │
                         └──────────────┬───────────────────────────────┘
                                        │ HTTP / SSE
                         ┌──────────────▼───────────────────────────────┐
                         │        API GATEWAY (port 5000)                │
                         │  JWT Auth · SSE Stream · Routing              │
                         │  Request Logging · Health Checks              │
                         └──────────────┬───────────────────────────────┘
                                        │
              ┌─────────────────────────┼──────────────────────────────────┐
              │                         │                         │        │
  ┌───────────▼──────────┐  ┌──────────▼──────────┐  ┌─────────▼──────────┐  ┌───────────────────┐
  │ TRANSACTION SERVICE  │  │    RISK SERVICE     │  │  PAYMENT SERVICE   │  │ PIN ENCRYPTION    │
  │     (port 5001)      │  │    (port 5002)      │  │    (port 5003)     │  │ SERVICE (5005)    │
  │ Create & validate    │  │ Risk scoring engine │  │ Authorization      │  │ ISO 8583 msgs     │
  │ Persist to SQL Server│  │ Velocity detection  │  │ Amount limits      │  │ HSM key mgmt      │
  │ Publish events       │  │ Anomaly flagging    │  │ PIN verification   │  │ PIN block encrypt │
  └───────────┬──────────┘  └──────────┬──────────┘  └──────────┬─────────┘  └────────┬──────────┘
              │                         │                         │                     │
              └─────────────────────────┼─────────────────────────┘─────────────────────┘
                                        │
                         ┌──────────────▼───────────────────────┐
                         │     COMPLIANCE SERVICE (port 5004)    │
                         │  SHA-256 hash chain audit logs        │
                         │  Prometheus metrics                   │
                         │  Persist to SQL Server                │
                         └──────────────────────────────────────┘
```

### Event Pipeline

Every transaction flows through the pipeline, each stage producing an event:

**Non-card transactions (wire transfers, etc.):**
```
TransactionCreated → RiskEvaluated → PaymentAuthorized → AuditLogged
```

**Card transactions (with PAN + PIN block):**
```
TransactionCreated ──► PinVerified ──┐
        │                            ├─► PaymentAuthorized ──► AuditLogged
        └──► RiskEvaluated ──────────┘
```

If PIN verification fails, the payment is rejected regardless of risk score. All events are broadcast via Server-Sent Events (SSE) to connected frontend dashboards in real time.

### Adaptive Event Bus

The system can switch between messaging backends based on CPU load, or pin a specific backend via configuration:

| CPU Load | Backend | Use Case |
|----------|---------|----------|
| < 25% | InMemory | Development / idle |
| 25-50% | Redis Streams | Low-medium traffic |
| 50-75% | RabbitMQ | Reliable delivery |
| >= 75% | Kafka | High throughput |

In Docker, Redis is the default backend, enabling cross-service event propagation across containers.

---

## Features

### Backend

- **JWT Authentication** - Login/register with role-based access (Guest, User, Admin, Auditor)
- **Transaction Management** - Create, query, and list transactions with decimal precision
- **Risk Scoring Engine** - Rule-based scoring (amount, velocity, time-of-day) with HIGH/MEDIUM/LOW levels
- **Payment Authorization** - Simulated gateway with configurable rules (amount limits, risk thresholds, PIN verification)
- **Audit Trail** - Immutable event log with SHA-256 hash chain integrity verification
- **Server-Sent Events** - Real-time event streaming to all connected clients
- **Adaptive Event Bus** - CPU-based auto-switching between InMemory/Redis/RabbitMQ/Kafka backends
- **Distributed Backends** - Redis Streams (XADD/XREADGROUP), RabbitMQ (fanout exchange), Kafka (topics + consumer groups)
- **PIN Encryption Service** - ISO 9564 Format 0 PIN block operations, simulated HSM with AES-256 key management
- **ISO 8583 Messaging** - Full parser/builder for financial transaction card-originated interchange messages
- **Card Authorization** - ISO 8583 0100 authorization flow with deny-list support and PIN verification
- **Structured Logging** - Serilog with console sink, request logging middleware
- **Prometheus Metrics** - Compliance service exposes `/metrics` endpoint
- **Request Logging Middleware** - Every HTTP request/response logged with method, path, status code, and duration

### Frontend

- **Transaction UI** - Submit transactions, view table with KPI cards, real-time SSE live feed
- **Admin Dashboard** - Service health monitoring, event bus status, system metrics
- **Risk Dashboard** - Risk gauge visualization, distribution pie chart, 24-hour trend line, events table
- **Audit Dashboard** - Hash chain timeline, filterable event log, chain link visualization, integrity display
- **Card Operations** - HSM key management, PIN encrypt/decrypt/verify, ISO 8583 message tools, card authorization

---

## Tech Stack

| Layer | Technology | Purpose |
|-------|-----------|---------|
| Backend | .NET 10, ASP.NET Core | Microservice APIs |
| Database | SQL Server 2022 | Persistent storage |
| ORM | Entity Framework Core 10 | Code-first database access |
| Auth | JWT Bearer tokens | Stateless authentication |
| Logging | Serilog | Structured logging |
| Metrics | prometheus-net | Application metrics |
| Event Bus | Custom adaptive layer | Multi-backend messaging (Redis/RabbitMQ/Kafka) |
| Real-time | Server-Sent Events (SSE) | Push updates to frontends |
| Frontend | React 19, TypeScript | UI applications |
| Build | Vite 6 | Fast frontend bundling |
| Styling | Tailwind CSS v4 | Utility-first CSS |
| State | Zustand | Client-side state management |
| Charts | Recharts | Data visualization |
| Icons | Lucide React | SVG icon library |
| Testing (.NET) | xUnit, Moq | Unit and integration tests |
| Testing (React) | Vitest, Testing Library | Component and store tests |
| Containers | Docker, Docker Compose | Multi-container orchestration |
| Monitoring | Prometheus, Grafana | Metrics collection and dashboards |

---

## Prerequisites

### For Docker (recommended)

- [Docker Desktop](https://www.docker.com/products/docker-desktop/) (includes Docker Compose)
- 8 GB RAM minimum (SQL Server container requires ~2 GB)
- 15 GB free disk space for images

### For local development

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- [Node.js 20+](https://nodejs.org/) (for frontend apps)
- [SQL Server 2022](https://www.microsoft.com/en-us/sql-server/sql-server-downloads) (Developer edition is free) - or use Docker for just the database
- [Visual Studio 2022 v17.14+](https://visualstudio.microsoft.com/) (optional, for IDE experience)

---

## Quick Start (Docker)

This starts **all 18 services** - SQL Server, Redis, RabbitMQ, Kafka, Zookeeper, 6 .NET microservices, 5 React frontends, Prometheus, and Grafana - with a single command.

### 1. Clone and navigate to the infrastructure directory

```bash
git clone <repo-url>
cd adaptive-realtime-financial-transaction-platform/infrastructure
```

### 2. Start everything

```bash
docker-compose up --build
```

Add `-d` to run in the background:

```bash
docker-compose up --build -d
```

### 3. Wait for services to be healthy

SQL Server takes ~30 seconds to start. The .NET services wait for it automatically via healthcheck dependencies. You can monitor progress:

```bash
docker-compose logs -f transaction-service
```

### 4. Access the platform

| Service | URL | Description |
|---------|-----|-------------|
| **Transaction UI** | http://localhost:3001 | Submit transactions, view dashboard |
| **Admin Dashboard** | http://localhost:3002 | Service health monitoring |
| **Risk Dashboard** | http://localhost:3003 | Risk score visualization |
| **Audit Dashboard** | http://localhost:3004 | Audit trail and hash chain |
| **Card Operations** | http://localhost:3005 | HSM key management, PIN operations, ISO 8583 tools |
| API Gateway | http://localhost:5000/swagger | Interactive Swagger UI |
| Transaction Service | http://localhost:5001/swagger | Interactive Swagger UI |
| Risk Service | http://localhost:5002/swagger | Interactive Swagger UI |
| Payment Service | http://localhost:5003/swagger | Interactive Swagger UI |
| Compliance Service | http://localhost:5004/swagger | Interactive Swagger UI |
| PIN Encryption Service | http://localhost:5005/swagger | HSM & ISO 8583 API |
| Grafana | http://localhost:3000 | admin/admin |
| Prometheus | http://localhost:9090 | Metrics browser |
| RabbitMQ Management | http://localhost:15672 | guest/guest |

### 5. Stop everything

```bash
docker-compose down          # Stop containers (keeps data volumes)
docker-compose down -v       # Stop containers AND delete database data
```

### Running specific services

```bash
# Start only SQL Server and the database-dependent services
docker-compose up sqlserver transaction-service compliance-service

# Start only infrastructure (no .NET services, no frontends)
docker-compose up sqlserver redis rabbitmq kafka prometheus grafana

# Start just the database (for local .NET development)
docker-compose up sqlserver
```

---

## Running Locally without Docker

### 1. Start SQL Server

Option A - Use Docker for just the database:

```bash
cd infrastructure
docker-compose up sqlserver
```

Option B - Use a local SQL Server installation. Update the connection strings in each service's `appsettings.json` if your instance differs from `localhost,1433`.

### 2. Start the backend services

Open 6 terminal windows and run each service:

```bash
cd backend

# Terminal 1 - API Gateway
dotnet run --project src/FinancialPlatform.ApiGateway            # port 5000

# Terminal 2 - Transaction Service
dotnet run --project src/FinancialPlatform.TransactionService    # port 5001

# Terminal 3 - Risk Service
dotnet run --project src/FinancialPlatform.RiskService           # port 5002

# Terminal 4 - Payment Service
dotnet run --project src/FinancialPlatform.PaymentService        # port 5003

# Terminal 5 - Compliance Service
dotnet run --project src/FinancialPlatform.ComplianceService     # port 5004

# Terminal 6 - PIN Encryption Service
dotnet run --project src/FinancialPlatform.PinEncryptionService  # port 5005
```

Database tables are created automatically on first startup (EF Core migrations are applied via `MigrateAsync()`).

> **Note:** When running locally without Docker, each service uses an InMemory event bus by default. Events flow within each service but **do not** propagate across services. To enable cross-service events locally, start Redis (`docker-compose up redis`) and set the environment variable `EventBus__DefaultBackend=Redis` for each service.

### 3. Start the frontend apps

Open 5 more terminal windows:

```bash
# Transaction UI (port 3001)
cd frontend/apps/transaction-ui && npm install && npm run dev

# Admin Dashboard (port 3002)
cd frontend/apps/admin-dashboard && npm install && npm run dev

# Risk Dashboard (port 3003)
cd frontend/apps/risk-dashboard && npm install && npm run dev

# Audit Dashboard (port 3004)
cd frontend/apps/audit-dashboard && npm install && npm run dev

# Card Operations (port 3005)
cd frontend/apps/card-operations && npm install && npm run dev
```

---

## Using the Platform - Step by Step

### Step 1: Register a User

All authenticated operations require a JWT token. Register a user first:

```bash
curl -X POST http://localhost:5000/api/auth/register \
  -H "Content-Type: application/json" \
  -d '{
    "username": "trader1",
    "password": "password123",
    "role": "User"
  }'
```

Response:
```json
{
  "message": "User registered successfully",
  "userId": "a1b2c3d4-..."
}
```

Valid roles: `Guest`, `User`, `Admin`, `Auditor`

A default admin user `admin` / `admin123` is always available.

### Step 2: Login and Get a JWT Token

```bash
curl -X POST http://localhost:5000/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{"username": "admin", "password": "admin123"}'
```

Response:
```json
{
  "token": "eyJhbGciOiJIUzI1NiIs...",
  "role": "Admin",
  "expiresAt": "2025-01-01T13:00:00Z"
}
```

The token expires in 1 hour. Include it in subsequent requests as `Authorization: Bearer <token>`.

### Step 3: Create a Transaction

```bash
curl -X POST http://localhost:5001/api/transactions \
  -H "Content-Type: application/json" \
  -d '{
    "userId": "user_001",
    "amount": 250.00,
    "currency": "USD",
    "description": "Invoice payment",
    "counterparty": "merchant_abc"
  }'
```

Response (201 Created):
```json
{
  "id": "f47ac10b-...",
  "userId": "user_001",
  "amount": 250.00,
  "currency": "USD",
  "status": "Pending",
  "timestamp": "2025-01-01T12:00:00Z",
  "description": "Invoice payment",
  "counterparty": "merchant_abc"
}
```

This triggers the full event pipeline automatically:
1. **TransactionCreatedEvent** is published
2. Risk Service picks it up and evaluates risk
3. **RiskEvaluatedEvent** is published with a score and level
4. Payment Service processes the risk result
5. **PaymentAuthorizedEvent** is published (approved or rejected)
6. Compliance Service logs all events to the immutable audit trail
7. **AuditLoggedEvent** is published for each logged event

### Step 3b: Create a Card Transaction (with PIN)

Card transactions include a PAN (card number) and encrypted PIN block, which triggers PIN verification:

```bash
curl -X POST http://localhost:5001/api/transactions \
  -H "Content-Type: application/json" \
  -d '{
    "userId": "user_001",
    "amount": 150.00,
    "currency": "USD",
    "description": "Card payment",
    "counterparty": "merchant_xyz",
    "pan": "4111111111111111",
    "pinBlock": "AABBCCDDEEFF0011",
    "cardType": "Credit"
  }'
```

The card transaction pipeline includes an additional step:
1. **TransactionCreatedEvent** is published (with PAN + PIN block)
2. Risk Service evaluates risk as usual → **RiskEvaluatedEvent**
3. PIN Encryption Service decrypts the PIN block → **PinVerifiedEvent**
4. Payment Service checks **both** the risk score and PIN verification result
5. If PIN verification failed → payment is rejected regardless of risk score
6. **PaymentAuthorizedEvent** is published (approved or rejected)
7. Compliance Service logs all events including PinVerifiedEvent

### Step 4: View Transactions

```bash
# List all transactions (newest first)
curl http://localhost:5001/api/transactions

# Get a specific transaction
curl http://localhost:5001/api/transactions/{id}

# Get transactions for a specific user
curl http://localhost:5001/api/transactions/user/user_001
```

### Step 5: Manually Evaluate Risk

You can trigger a manual risk evaluation outside the event pipeline:

```bash
curl -X POST http://localhost:5002/api/risk/evaluate \
  -H "Content-Type: application/json" \
  -d '{
    "transactionId": "tx_manual_001",
    "userId": "user_001",
    "amount": 7500.00,
    "currency": "EUR"
  }'
```

Response (202 Accepted - evaluation is asynchronous):
```json
{
  "message": "Risk evaluation initiated",
  "transactionId": "tx_manual_001"
}
```

### Step 6: Check Payment Authorization

Test the payment gateway rules directly:

```bash
curl -X POST http://localhost:5003/api/payment/authorize \
  -H "Content-Type: application/json" \
  -d '{
    "amount": 7500.00,
    "riskScore": 55
  }'
```

Response:
```json
{
  "authorized": false,
  "reason": "High amount with elevated risk",
  "amount": 7500.00,
  "riskScore": 55,
  "timestamp": "2025-01-01T12:00:00Z"
}
```

### Step 7: View the Audit Trail

```bash
# Paginated audit logs (default: page 1, 20 per page)
curl http://localhost:5004/api/audit

# Page 2 with 10 items per page
curl "http://localhost:5004/api/audit?page=2&pageSize=10"

# Get a specific audit log
curl http://localhost:5004/api/audit/{id}
```

Response:
```json
{
  "data": [
    {
      "id": "...",
      "eventType": "TransactionCreatedEvent",
      "payload": "{\"transactionId\":\"...\",\"userId\":\"user_001\",...}",
      "previousHash": "0000000000000000000000000000000000000000000000000000000000000000",
      "hash": "a3f2b8c1...",
      "createdAt": "2025-01-01T12:00:00Z"
    }
  ],
  "page": 1,
  "pageSize": 20,
  "total": 47,
  "totalPages": 3
}
```

### Step 8: Verify Audit Chain Integrity

```bash
curl http://localhost:5004/api/audit/verify
```

Response (valid chain):
```json
{
  "valid": true,
  "checkedCount": 47,
  "brokenLinks": [],
  "message": "Hash chain integrity verified successfully."
}
```

Response (tampered chain):
```json
{
  "valid": false,
  "checkedCount": 47,
  "brokenLinks": [
    {
      "logId": "...",
      "index": 12,
      "issue": "Hash mismatch"
    }
  ],
  "message": "Hash chain has 1 broken link(s)."
}
```

### Step 9: View Audit Statistics

```bash
curl http://localhost:5004/api/audit/stats
```

Response:
```json
{
  "totalCount": 47,
  "byEventType": [
    { "eventType": "TransactionCreatedEvent", "count": 12 },
    { "eventType": "RiskEvaluatedEvent", "count": 12 },
    { "eventType": "PaymentAuthorizedEvent", "count": 12 },
    { "eventType": "AuditLoggedEvent", "count": 11 }
  ],
  "earliestLog": { "id": "...", "createdAt": "2025-01-01T12:00:00Z" },
  "latestLog": { "id": "...", "createdAt": "2025-01-01T12:30:00Z" }
}
```

### Step 10: Check System Health

```bash
# Public health check (no auth required)
curl http://localhost:5000/api/gateway/health
```

Response:
```json
{
  "status": "Healthy",
  "timestamp": "2025-01-01T12:00:00Z",
  "services": {
    "apiGateway": { "status": "Healthy", "port": 5000 },
    "transactionService": { "status": "Unknown", "url": "http://transaction-service:5001" },
    "riskService": { "status": "Unknown", "url": "http://risk-service:5002" },
    "paymentService": { "status": "Unknown", "url": "http://payment-service:5003" },
    "complianceService": { "status": "Unknown", "url": "http://compliance-service:5004" }
  }
}
```

### Step 11: Check Authenticated System Status

```bash
curl http://localhost:5000/api/gateway/status \
  -H "Authorization: Bearer <your-jwt-token>"
```

Response:
```json
{
  "activeSseConnections": 3,
  "eventBusBackend": "Redis",
  "timestamp": "2025-01-01T12:00:00Z"
}
```

### Step 12: Subscribe to Real-Time Events (SSE)

Connect to the Server-Sent Events stream to receive live updates:

```bash
curl -N http://localhost:5000/api/sse/stream
```

Or open in a browser / JavaScript EventSource:

```javascript
const source = new EventSource('http://localhost:5000/api/sse/stream');
source.onmessage = (event) => {
  const data = JSON.parse(event.data);
  console.log('Event:', data);
};
```

Events are streamed in real time as transactions flow through the pipeline. The connection stays open until the client disconnects.

### Step 13: Use the Frontend Dashboards

All five frontends are single-page applications with a dark theme (black background, white text). Each runs on its own port and can be opened independently in any browser. No login is required for any frontend.

---

#### Transaction UI (http://localhost:3001)

The main user-facing app for submitting transactions and monitoring activity in real time. This is the only app connected to live backend APIs.

**Page layout (top to bottom):**

1. **Header bar** -- Shows "Transaction Dashboard" with a pulsing green "Live" indicator on the right, confirming the SSE connection is active.

2. **KPI Cards row** -- Four summary cards across the top:
   - **Total Transactions** (blue icon) -- count of all transactions
   - **Total Volume** (green icon) -- sum of all transaction amounts
   - **Average Amount** (amber icon) -- mean transaction value
   - **Pending** (red icon) -- count of transactions awaiting processing

   These cards auto-update every 30 seconds and immediately when you submit a new transaction.

3. **Transaction Form + Live Feed** -- Two-column section:
   - **Left: Transaction Form** -- Submit a new transaction:
     1. Enter a **User ID** (required, e.g., `user_001`)
     2. Enter an **Amount** (required, e.g., `250.00`)
     3. Select a **Currency** from the dropdown (USD, EUR, GBP, JPY, CAD, AUD, CHF, CNY)
     4. Optionally enter a **Description** and **Counterparty**
     5. Click **Submit Transaction** -- a green success banner appears, the form resets, and the event flows through the pipeline
     6. The button is disabled until both User ID and Amount are filled in; it shows a spinner while the request is processing
   - **Right: Live Feed** -- A scrolling list of real-time events streamed via SSE. Each event shows an icon (color-coded by type), a description, and a timestamp. The feed holds up to 50 events with newest on top.

4. **Transaction Table** -- Full-width table showing all transactions sorted newest-first:
   - **ID** -- truncated, monospace (click to copy)
   - **User** -- the user ID
   - **Amount** -- formatted to 2 decimal places, bold
   - **Currency** -- 3-letter code
   - **Status** -- color-coded pill badge:
     - Green = completed/success
     - Amber = pending
     - Red = failed/rejected
     - Blue = processing
   - **Timestamp** -- local date and time

---

#### Admin Dashboard (http://localhost:3002)

A system monitoring dashboard showing service health, event bus status, and metrics. Currently uses mock data for demonstration.

**Page layout (top to bottom):**

1. **Header** -- "Admin Control Panel" with a "Last updated" timestamp on the right.

2. **System Status** -- Five service cards in a responsive grid (one per backend service):
   - Each card shows: service name, colored health dot (green=healthy, amber=degraded, red=down), port number, and uptime percentage
   - Services: API Gateway (5000), Transaction (5001), Risk (5002), Payment (5003), Compliance (5004)

3. **Event Bus & SSE Connections** -- Two side-by-side panels:
   - **Left: Event Bus Status** -- Shows the currently active messaging backend (InMemory, Redis, RabbitMQ, or Kafka). Each backend is displayed as a pill tag; the active one has a blue highlight. Colored indicator dots distinguish backends (gray=InMemory, red=Redis, amber=RabbitMQ, blue=Kafka)
   - **Right: SSE Connections** -- Shows total connected SSE clients, broken down by event type (Transaction Events, Risk Events, Audit Events, Payment Events)

4. **Quick Actions** -- Three buttons:
   - **Run Health Check** (blue button with refresh icon) -- simulates a health check with a 2-second loading spinner
   - **Clear Cache** (dark button with trash icon) -- shows "Cache Cleared!" for 1.5 seconds
   - **View Metrics** (dark button with chart icon) -- toggles open a metrics panel showing CPU Usage, Memory, Requests/sec, and Average Latency with colored progress bars

---

#### Risk Dashboard (http://localhost:3003)

A risk monitoring dashboard with visual gauges, charts, and an event table. Currently uses mock data for demonstration.

**Page layout (top to bottom):**

1. **Header** -- "Risk Monitoring Dashboard" with a green "Live" indicator.

2. **Alert Banner** -- A red warning bar appears at the top when HIGH-risk transactions are detected. Shows the count of high-risk events and recommends immediate review. Disappears when there are no high-risk events.

3. **Risk Score Gauge + Distribution** -- Two-column section:
   - **Left: Risk Gauge** -- A semicircular SVG dial with three color zones (green=LOW 0-49, amber=MEDIUM 50-79, red=HIGH 80-100). A needle points to the current aggregate risk score. Hover to see the exact number
   - **Right: Risk Distribution Pie Chart** -- A donut chart showing the breakdown of risk levels. Three segments: LOW (green), MEDIUM (amber), HIGH (red). Hover over each segment to see the count and percentage

4. **24-Hour Risk Trend** -- A full-width line chart showing how risk scores changed over the past 24 hours in 2-hour intervals. Hover over data points to see exact scores. The chart uses the Recharts library with interactive tooltips.

5. **Recent Risk Events Table** -- A full-width table listing individual risk evaluations:
   - **Transaction ID** -- blue monospace text
   - **Score** -- colored by risk level (green/amber/red thresholds)
   - **Level** -- colored badge (HIGH/MEDIUM/LOW)
   - **Flags** -- small pills showing detected risk factors (velocity, amount-threshold, odd-hour, geo-anomaly, pattern-match, new-device, cross-border). Shows "--" when no flags
   - **Time** -- timestamp

---

#### Audit Dashboard (http://localhost:3004)

An audit trail visualization dashboard with hash chain integrity display, event filtering, and search. Currently uses mock data for demonstration.

**Page layout (top to bottom):**

1. **Header** -- "Audit Trail Dashboard" with a green "Chain Verified" indicator showing hash chain integrity.

2. **Statistics Cards** -- Three cards across the top:
   - **Total Events** -- total number of audit log entries
   - **Chain Integrity** -- shows verified vs. tampered count with a colored shield icon (green=verified, red=tampered)
   - **Events Today** -- count of events recorded today

3. **Search and Filters** -- A toolbar with:
   - **Filter buttons** -- Five buttons: "All", "TransactionCreated", "RiskEvaluated", "PaymentAuthorized", "AuditLogged". Click to filter the timeline to one event type. The active filter has a blue highlight
   - **Search input** -- A full-width text field with a magnifying glass icon. Type to filter events by hash value, event type, or JSON payload content (case-insensitive). The timeline and chain diagram update instantly as you type. Filters and search work together (AND logic)

4. **Hash Chain Timeline** -- A vertical timeline on the left side. Each event is a card with:
   - A colored circle with an icon indicating the event type (blue=TransactionCreated, amber=RiskEvaluated, green=PaymentAuthorized, purple=AuditLogged)
   - Event type badge and timestamp
   - **Hash** value in blue monospace
   - **Previous Hash** in gray with a chain-link icon
   - JSON payload displayed in gray monospace
   - Vertical connecting lines between events showing the chain relationship

5. **Chain Link Diagram** -- A horizontal visualization at the bottom. All events are shown as connected boxes with truncated hash values and colored event type labels, linked by arrows. Horizontally scrollable when content overflows.

---

#### Card Operations (http://localhost:3005)

A card payment operations toolkit for HSM key management, PIN encryption operations, and ISO 8583 message tools. This app connects directly to the PIN Encryption Service (port 5005) and uses live APIs.

**What is an HSM?**

Imagine you have a super-secure safe that only knows how to do one thing: lock and unlock secrets. Nobody can open the safe to peek inside, you just hand it a secret through a slot, and it hands back a locked box. That's basically an **HSM** (Hardware Security Module). Banks use real HSMs — physical, tamper-proof boxes, to protect things like your credit card PIN. Our platform simulates one in software so you can learn how it works.

The HSM holds special keys inside it. Think of them as different types of master keys on a keyring:

| Key Type | Full Name | ELI5 Analogy | What It Does |
|----------|-----------|-------------|--------------|
| **ZPK** | Zone PIN Key | A locked briefcase for carrying PINs | When you type your PIN at an ATM, it needs to travel safely to your bank. A ZPK scrambles (encrypts) the PIN so nobody can read it while it's traveling, like putting it in a locked briefcase. Only the bank has the key to open it. |
| **ZMK** | Zone Master Key | The key that opens the briefcase factory | Banks don't share ZPKs directly, that would be risky. Instead, they use a ZMK (a bigger, stronger key) to lock up the ZPK itself before sending it over. Think of it as shipping a key inside a tamper-proof container. The recipient uses their copy of the ZMK to unwrap the ZPK. |
| **PVK** | PIN Verification Key | A mold that checks if a key fits | When you enter your PIN, the bank needs to check it's correct without actually storing your real PIN (that would be dangerous). A PVK helps create a mathematical "fingerprint" of your PIN. The bank stores only the fingerprint. When you type your PIN next time, it makes a new fingerprint and checks: do they match? |
| **CVK** | Card Verification Key | The secret recipe printed on the back of your card | You know those 3 digits on the back of your card (CVV)? A CVK is the secret ingredient the bank uses to generate those digits. Every card has a unique CVV because it's made from a combination of your card number, expiry date, and this secret key. If someone makes a fake card with the right number but wrong CVV, the CVK catches it. |
| **TAK** | Terminal Authentication Key | A secret handshake between the ATM and the bank | When an ATM talks to the bank's computer, they need to make sure nobody is pretending to be the ATM. A TAK is like a secret handshake — both sides know it, and they use it to prove "yes, it's really me" before sharing any sensitive data. |

**Technical Explanation: How Each Key Works**

This section explains what each key type does internally, in terms a developer can follow without needing to read the source code.

**ZPK (Zone PIN Key)**
- A symmetric AES-256 key shared between two entities (e.g., an ATM network and a bank).
- When you type your PIN at an ATM, the ATM's HSM combines your PIN digits with your card number (PAN) using the ISO 9564 Format 0 algorithm to produce a 16-character hex PIN block. It then encrypts that block with the ZPK.
- The encrypted block is what travels over the network. The receiving side decrypts it with the same ZPK to recover the PIN.
- In this platform: the simulated HSM stores ZPKs in an in-memory dictionary. `POST /api/hsm/pin/encrypt` takes a PIN + PAN, formats them into an ISO 9564 block, and encrypts with AES-256-CBC. The inverse operation is `POST /api/hsm/pin/decrypt`.

**ZMK (Zone Master Key)**
- A key-encrypting key (KEK). Its only job is to encrypt other keys (like ZPKs) so they can be safely transmitted between systems.
- The flow: System A generates a new ZPK, encrypts it under the ZMK, and sends the encrypted result to System B. System B decrypts it under its copy of the ZMK, and now both sides have the same ZPK without it ever crossing the wire in cleartext.
- In this platform: the HSM service supports `POST /api/hsm/pin/translate` which re-encrypts a PIN block from one ZPK to another, simulating the key-exchange workflow.

**PVK (PIN Verification Key)**
- Used in the PVV (PIN Verification Value) or PVK-derived PIN offset method. The bank never stores your actual PIN; instead it stores a small verification value derived from your PIN + PAN + PVK.
- At verification time, the HSM derives a new value from the submitted PIN and compares it to the stored one. If they match, the PIN is correct.
- In this platform: `POST /api/hsm/pin/verify` decrypts the submitted PIN block, extracts the cleartext PIN, and compares it against the expected PIN you provide.

**CVK (Card Verification Key)**
- Used to generate and verify CVV (Card Verification Value) or CVC (Card Validation Code), the 3-digit number on the back of payment cards.
- The CVV is derived from: card number + expiry date + service code + CVK, using a payment-network-defined algorithm (similar to a keyed hash). This ensures only the card issuer (who holds the CVK) can generate valid CVVs.
- In this platform: CVK keys can be generated and stored in the simulated HSM alongside the other key types. They share the same AES-256 storage mechanism.

**TAK (Terminal Authentication Key)**
- A symmetric key shared between a payment terminal (ATM, POS device) and the acquiring host.
- Used to generate and verify a MAC (Message Authentication Code) on every message exchanged. The MAC is a short cryptographic digest that proves the message was not tampered with and came from a terminal holding the same TAK.
- In this platform: TAK keys are generated and listed alongside other key types. They demonstrate that the HSM can manage multiple key categories within a single key store.

**How key generation and storage works in this platform**

1. You select a key type (ZPK, ZMK, PVK, CVK, TAK) and provide a unique key ID.
2. The HSM service generates a random 256-bit key using a cryptographically secure random number generator.
3. The key is stored in an in-memory dictionary keyed by the ID you chose. A KCV (Key Check Value) is computed by encrypting a block of zeros with the new key and taking the first few hex characters. This lets two parties verify they loaded the same key without ever transmitting the key itself.
4. On startup, a default ZPK called `default-zpk` is seeded automatically so PIN operations work immediately.

**Page layout (top to bottom):**

1. **Header** -- "Card Operations" with "ISO 8583 . HSM . PIN Encryption" subtitle.

2. **HSM Key Management + PIN Operations** -- Two-column section:
   - **Left: HSM Key Management**
     - Active keys are displayed as blue pill badges at the top (e.g., `default-zpk`). The list auto-refreshes every 30 seconds
     - To generate a new key:
       1. Select a **Key Type** from the dropdown (ZPK, ZMK, PVK, CVK, TAK)
       2. Enter a **Key ID** (e.g., `my-zpk-001`)
       3. Click **Generate Key** -- success shows the key ID and KCV (Key Check Value) in green; errors show in red
     - The button is disabled until a Key ID is entered
   - **Right: PIN Operations** -- Three sub-sections, each with its own inputs and button:
     - **Encrypt PIN** -- Enter a cleartext PIN (e.g., `1234`) and a PAN (card number, e.g., `4111111111111111`). Click "Encrypt PIN" to get the encrypted PIN block displayed in green monospace hex
     - **Decrypt PIN Block** -- Paste an encrypted PIN block (16 hex chars) and enter the PAN. Click "Decrypt PIN Block" to see the decrypted PIN displayed as "PIN: 1234" in green
     - **Verify PIN** -- Enter an encrypted PIN block, PAN, and the expected PIN. Click "Verify PIN" -- shows a green "PIN Verified" panel if the PIN matches, or a red "PIN Mismatch" panel if it does not
     - All buttons are disabled until the required fields are filled in; they show a spinner while processing

3. **ISO 8583 Tools** -- Full-width section with a three-column grid:
   - **Parse Message** -- Paste a raw ISO 8583 message string into the textarea and click "Parse" to see the MTI (e.g., "0100 - Authorization Request") and a breakdown of all parsed fields with their names and values
   - **Build Message** -- Enter an MTI code (defaults to "0100") and a JSON map of field numbers to values (e.g., `{"2": "4111111111111111", "4": "000000001000", "49": "840"}`). Click "Build" to see the assembled ISO 8583 message in green monospace
   - **Card Authorization** -- A full 0100 authorization simulation:
     1. Enter a **PAN** (defaults to `4111111111111111`)
     2. Enter an **Amount** (defaults to `100.00`)
     3. Enter a **Currency** (defaults to `USD`)
     4. Optionally enter an **Encrypted PIN Block** (leave blank to skip PIN verification)
     5. Click **Authorize Card**
     6. Results show as a green "APPROVED" panel with response code, authorization ID, and message, or a red "DECLINED" panel. PANs starting with `4999` are always declined (test deny-list)

---

#### Quick navigation reference

| URL | App | Connected to | Live data |
|-----|-----|-------------|-----------|
| http://localhost:3001 | Transaction UI | API Gateway (5000) | Yes (SSE + polling) |
| http://localhost:3002 | Admin Dashboard | None (mock data) | No |
| http://localhost:3003 | Risk Dashboard | None (mock data) | No |
| http://localhost:3004 | Audit Dashboard | None (mock data) | No |
| http://localhost:3005 | Card Operations | PIN Encryption Service (5005) | Yes (polling) |

---

## API Reference

### Authentication (`API Gateway :5000`)

| Method | Endpoint | Auth | Description |
|--------|----------|------|-------------|
| POST | `/api/auth/login` | No | Login with username/password, returns JWT |
| POST | `/api/auth/register` | No | Register a new user with role |

### Gateway (`API Gateway :5000`)

| Method | Endpoint | Auth | Description |
|--------|----------|------|-------------|
| GET | `/api/gateway/health` | No | Health check for all services |
| GET | `/api/gateway/status` | Yes | SSE connections and event bus status |
| GET | `/api/sse/stream` | No | Server-Sent Events stream (real-time updates) |

### Transactions (`Transaction Service :5001`)

| Method | Endpoint | Auth | Description |
|--------|----------|------|-------------|
| POST | `/api/transactions` | No | Create a new transaction |
| GET | `/api/transactions` | No | List all transactions (newest first) |
| GET | `/api/transactions/{id}` | No | Get transaction by ID |
| GET | `/api/transactions/user/{userId}` | No | Get transactions for a user |

### Risk (`Risk Service :5002`)

| Method | Endpoint | Auth | Description |
|--------|----------|------|-------------|
| GET | `/api/risk/health` | No | Service health check |
| POST | `/api/risk/evaluate` | No | Manually trigger risk evaluation |

### Payment (`Payment Service :5003`)

| Method | Endpoint | Auth | Description |
|--------|----------|------|-------------|
| GET | `/api/payment/health` | No | Service health check |
| GET | `/api/payment/status/{transactionId}` | No | Check payment status |
| POST | `/api/payment/authorize` | No | Manual authorization check |

### Audit (`Compliance Service :5004`)

| Method | Endpoint | Auth | Description |
|--------|----------|------|-------------|
| GET | `/api/audit?page=1&pageSize=20` | No | Paginated audit logs (1-100 per page) |
| GET | `/api/audit/{id}` | No | Get specific audit log |
| GET | `/api/audit/verify` | No | Verify hash chain integrity |
| GET | `/api/audit/stats` | No | Audit statistics by event type |

### HSM (`PIN Encryption Service :5005`)

| Method | Endpoint | Auth | Description |
|--------|----------|------|-------------|
| GET | `/api/hsm/health` | No | Service health check with key count |
| GET | `/api/hsm/keys` | No | List all stored key IDs |
| POST | `/api/hsm/keys/generate` | No | Generate a new key (ZMK/ZPK/PVK/CVK/TAK) |
| POST | `/api/hsm/pin/encrypt` | No | Encrypt a PIN under a ZPK |
| POST | `/api/hsm/pin/decrypt` | No | Decrypt a PIN block |
| POST | `/api/hsm/pin/translate` | No | Translate PIN from one ZPK to another |
| POST | `/api/hsm/pin/verify` | No | Verify a PIN against expected value |

### ISO 8583 (`PIN Encryption Service :5005`)

| Method | Endpoint | Auth | Description |
|--------|----------|------|-------------|
| GET | `/api/iso8583/fields` | No | List supported ISO 8583 field definitions |
| POST | `/api/iso8583/parse` | No | Parse a raw ISO 8583 message |
| POST | `/api/iso8583/build` | No | Build a raw ISO 8583 message |
| POST | `/api/iso8583/authorize` | No | Full card authorization with PIN |

---

## Event Flow and Business Rules

### Transaction Lifecycle

**Non-card transactions:**
```
1. POST /api/transactions          → TransactionCreatedEvent
2. Risk Service subscribes         → RiskEvaluatedEvent (score + level + flags)
3. Payment Service subscribes      → PaymentAuthorizedEvent (authorized/rejected)
4. Compliance Service subscribes   → AuditLogged (SHA-256 hash chain entry)
```

**Card transactions (with PAN + PIN block):**
```
1. POST /api/transactions              → TransactionCreatedEvent (with PAN + PIN block)
2. Risk Service subscribes             → RiskEvaluatedEvent (score + level + flags)
3. PIN Encryption Service subscribes   → PinVerifiedEvent (PIN decrypted and validated)
4. Payment Service checks both         → PaymentAuthorizedEvent (considers risk + PIN result)
5. Compliance Service subscribes       → AuditLogged (all events including PinVerified)
```

Each event is also logged by the Compliance Service, producing additional AuditLogged events. A single card transaction generates 5+ audit entries.

### Risk Scoring Algorithm

The risk score (0-100) is calculated from four factors:

| Factor | Condition | Points |
|--------|-----------|--------|
| High amount | Amount > 5,000 | +30 |
| Very high amount | Amount > 10,000 | +20 additional (50 total) |
| High velocity | > 5 transactions/min per user | +25 |
| Odd hours | Transaction between 10 PM - 6 AM | +15 |

Score is capped at 100. Risk levels: **HIGH** (>= 80), **MEDIUM** (>= 50), **LOW** (< 50)

Flags returned with each evaluation: `HIGH_AMOUNT`, `HIGH_VELOCITY`, `ODD_HOUR`

### Payment Authorization Rules

The payment gateway evaluates in this order (first match wins):

| Condition | Result | Reason |
|-----------|--------|--------|
| PIN verification failed | Rejected | PIN verification failed |
| Amount > 50,000 | Rejected | Amount exceeds daily limit |
| Risk score >= 80 | Rejected | Risk score too high |
| Amount > 5,000 AND risk >= 50 | Rejected | High amount with elevated risk |
| Otherwise | Approved | Authorized |

**Note:** PIN verification is only checked for card transactions (those with PAN + PIN block). For non-card transactions, the PIN check is skipped.

### Audit Hash Chain

Every event is logged with a SHA-256 hash that links to the previous entry:

```
Hash(n) = SHA256(Payload(n) + Hash(n-1))
```

- The first entry has `PreviousHash` set to all zeros (a null placeholder).
- Each subsequent entry chains to the one before it.
- Use `GET /api/audit/verify` to validate the entire chain.
- Any tampering with a log entry breaks the chain from that point forward.

---

## Frontend Applications

Each frontend is an independent Vite + React + TypeScript application with a high-contrast dark theme (`#0A0A0A` background, `#FFFFFF` text) using Tailwind CSS v4. All apps are single-page applications with no routing -- everything is visible on one scrollable page.

| App | URL | Data Source | Description |
|-----|-----|-------------|-------------|
| **Transaction UI** | http://localhost:3001 | Live API (API Gateway :5000) | Submit transactions, KPI cards, SSE live feed, transaction table |
| **Admin Dashboard** | http://localhost:3002 | Mock data | Service health cards, event bus status, SSE connections, quick actions |
| **Risk Dashboard** | http://localhost:3003 | Mock data | Risk score gauge, distribution pie chart, 24h trend line, events table |
| **Audit Dashboard** | http://localhost:3004 | Mock data | Hash chain timeline, event filters, search, chain link diagram |
| **Card Operations** | http://localhost:3005 | Live API (PIN Encryption Service :5005) | HSM key management, PIN encrypt/decrypt/verify, ISO 8583 tools, card authorization |

For detailed instructions on how to navigate and use each app, see **Step 13: Use the Frontend Dashboards** above.

---

## Swagger UI (Interactive API Docs)

Every backend service includes an interactive Swagger UI where you can explore and test API endpoints directly in your browser. No installation required.

### Accessing Swagger UI

| Service | Swagger URL |
|---------|-------------|
| API Gateway | http://localhost:5000/swagger |
| Transaction Service | http://localhost:5001/swagger |
| Risk Service | http://localhost:5002/swagger |
| Payment Service | http://localhost:5003/swagger |
| Compliance Service | http://localhost:5004/swagger |
| PIN Encryption Service | http://localhost:5005/swagger |

### How to Use Swagger UI

1. Open any Swagger URL above in your browser
2. You'll see a list of all API endpoints grouped by controller
3. Click any endpoint to expand it - you'll see the HTTP method, route, parameters, and response schemas
4. Click **Try it out** to enable editing
5. Fill in the required parameters or request body
6. Click **Execute** to send the request
7. The response body, status code, and headers are displayed below

### Authorizing Requests in Swagger UI

The API Gateway's Swagger UI includes a Bearer token authorizer:

1. First, use the `POST /api/auth/login` endpoint to get a JWT token
2. Copy the `token` value from the response
3. Click the **Authorize** button at the top of the page (lock icon)
4. Enter the token and click **Authorize**
5. Now all authenticated endpoints (like `GET /api/gateway/status`) will include the token automatically

### OpenAPI JSON Spec

The raw OpenAPI specification is available at `/openapi/v1.json` for each service (in Development environment). You can import these into Postman, code generators, or other API tools.

---

## Postman Collection

A comprehensive Postman collection is included for testing all API endpoints, with automated tests on every request.

### Files

| File | Location |
|------|----------|
| Collection | `postman/FinancialPlatform.postman_collection.json` |
| Docker environment | `postman/FinancialPlatform-Docker.postman_environment.json` |
| Local environment | `postman/FinancialPlatform-Local.postman_environment.json` |

### Importing into Postman

1. Open Postman
2. Click **Import** (top left)
3. Drag and drop all three JSON files from the `postman/` directory
4. Select the **Financial Platform - Docker** or **Financial Platform - Local** environment from the dropdown (top right)

### Collection Structure

The collection is organized into 6 folders with 25+ requests:

| Folder | Requests | Description |
|--------|----------|-------------|
| 1. Authentication | 5 | Register, Login, Invalid credentials, Invalid role |
| 2. Gateway | 3 | Health check, Authenticated status, Unauthorized check |
| 3. Transactions | 7 | Create (low/high amount), Zero amount (invalid), List, Get by ID, Get by user, Not found |
| 4. Risk Service | 4 | Health check, Evaluate (small/large amount), Missing fields |
| 5. Payment Service | 6 | Health check, Authorize (approved/rejected/daily limit/elevated risk), Payment status |
| 6. Compliance / Audit | 5 | Paginated logs, Get by ID, Verify chain, Statistics, Not found |

### Request Order (Recommended)

The collection is designed to run in order. Here's the recommended sequence:

1. **Register Admin** - Creates the admin user (may return 409 if already exists, that's OK)
2. **Login (Admin)** - Gets a JWT token and automatically saves it to the `auth_token` variable
3. **Create Transaction (Small Amount)** - Creates a $100 transaction, saves the transaction ID
4. **Create Transaction (Large Amount)** - Creates a $12,000 transaction (triggers HIGH risk)
5. **Get All Transactions** - Lists all transactions
6. **Get Transaction By ID** - Uses the saved transaction ID
7. **Get Transactions By User** - Filters by `user_postman`
8. **Evaluate Risk** - Manually triggers risk evaluation
9. **Authorize - Low Risk** - Tests payment approval ($100, score 20)
10. **Authorize - High Risk** - Tests payment rejection ($5K, score 85)
11. **Verify Hash Chain Integrity** - Validates the SHA-256 audit chain
12. **Audit Statistics** - Shows event type distribution

### Automated Tests

Every request includes automated test assertions that verify:
- Correct HTTP status codes (200, 201, 400, 401, 404)
- Response body structure (required fields exist)
- Business logic correctness (e.g., low risk is approved, high risk is rejected)
- Hash chain integrity
- JWT token extraction and variable propagation

### Running with Newman (CLI)

Newman is the command-line runner for Postman collections - ideal for CI/CD pipelines.

```bash
# Install Newman globally (if not already installed)
npm install -g newman

# Run against Docker environment
newman run postman/FinancialPlatform.postman_collection.json \
  -e postman/FinancialPlatform-Docker.postman_environment.json

# Run with detailed output
newman run postman/FinancialPlatform.postman_collection.json \
  -e postman/FinancialPlatform-Docker.postman_environment.json \
  --reporters cli,json \
  --reporter-json-export postman/results.json

# Run against local services
newman run postman/FinancialPlatform.postman_collection.json \
  -e postman/FinancialPlatform-Local.postman_environment.json
```

**Prerequisites for Newman:**
- All 5 backend services must be running (Docker or local)
- SQL Server must be accessible
- The first run will register the admin user and create test transactions

The platform supports four event bus backends, selectable via the `EventBus__DefaultBackend` environment variable.

### InMemory (default for local development)

Events are delivered in-process. No external infrastructure required. Events propagate only within a single service - suitable for development and testing.

### Redis Streams (default for Docker)

Uses Redis Streams with consumer groups for fan-out delivery.

- **Streams:** `events:{EventType}` (one stream per event type)
- **Consumer groups:** Service name as group name (e.g., `risk-service`)
- **Delivery:** XADD to publish, XREADGROUP to consume, XACK after handler success
- **At-least-once:** Unacknowledged messages stay pending for reprocessing

### RabbitMQ

Uses a fanout exchange with per-service durable queues.

- **Exchange:** `events` (fanout type, broadcasts to all bound queues)
- **Queues:** `FinancialPlatform.{serviceName}.{eventType}` (durable, survives restarts)
- **Delivery:** AsyncEventingBasicConsumer with push-based message delivery
- **At-least-once:** BasicAck on success, BasicNack with requeue on failure

### Kafka

Uses Kafka topics with consumer groups for distributed streaming.

- **Topics:** `events-{EventType}` (one topic per event type)
- **Consumer groups:** Service name as group ID (e.g., `payment-service`)
- **Delivery:** Blocking Consume() - true streaming, zero polling overhead
- **At-least-once:** Manual Commit() only after successful handler execution

### Switching Backends

In `docker-compose.yml`, change the environment variable for any service:

```yaml
environment:
  - EventBus__DefaultBackend=Redis       # or RabbitMQ, Kafka, InMemory
  - EventBus__RedisUrl=redis:6379
  - EventBus__RabbitMqUrl=amqp://guest:guest@rabbitmq:5672
  - EventBus__KafkaBrokers=kafka:9092
  - EventBus__ServiceName=transaction-service
```

Or set the environment variable when running locally:

```bash
export EventBus__DefaultBackend=Redis
export EventBus__RedisUrl=localhost:6379
dotnet run --project src/FinancialPlatform.TransactionService
```

---

## Observability (Prometheus + Grafana)

### Serilog Logging

Every service logs structured messages to the console. The API Gateway adds request logging middleware that logs every HTTP request with method, path, status code, and duration:

```
[12:00:00 INF] HTTP POST /api/transactions responded 201 in 13.23 ms
```

### Prometheus

Prometheus scrapes metrics from all backend services every 15 seconds. The Compliance Service exposes a `/metrics` endpoint.

- **Prometheus UI:** http://localhost:9090
- **Metrics endpoint:** http://localhost:5004/metrics

Query examples:
- `up` - shows which targets are being scraped
- `process_cpu_seconds_total` - CPU usage per service
- `dotnet_runtime_memory_bytes` - memory usage

### Grafana

Access at http://localhost:3000 (admin/admin). Pre-configured with:

- Prometheus as an auto-provisioned data source
- A dashboard with 8 panels (service health, request rates, latency, etc.)
- Auto-refresh every 5 seconds

### RabbitMQ Management UI

Access at http://localhost:15672 (guest/guest). Shows:

- Connections, channels, and consumers
- Queue depths and message rates
- Exchange bindings and routing

---

## Database

### Automatic Setup

When you start the Transaction Service or Compliance Service, EF Core automatically:
1. Creates the database if it does not exist
2. Applies all pending migrations (creates tables, indexes, etc.)

No manual `dotnet ef database update` is needed.

### Connection Details

| Property | Value |
|----------|-------|
| Host | `localhost` |
| Port | `1433` |
| Username | `sa` |
| Password | `YourStrong!Passw0rd` |
| Trust Certificate | Yes (self-signed in Docker) |

### Databases

| Database | Service | Tables |
|----------|---------|--------|
| `FinancialPlatform_Transactions` | Transaction Service | `Transactions`, `Users` |
| `FinancialPlatform_Compliance` | Compliance Service | `AuditLogs` |

### Viewing the Database

**Option A: SQL Server Management Studio (SSMS) or Azure Data Studio**
1. Connect to `localhost,1433` with `sa` / `YourStrong!Passw0rd`
2. Expand Databases to see tables and data

**Option B: Command Line (inside Docker container)**
```bash
docker exec -it financial_sqlserver /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "YourStrong!Passw0rd" -C

# List databases
SELECT name FROM sys.databases;
GO

# Query transactions
USE FinancialPlatform_Transactions;
SELECT TOP 10 * FROM Transactions ORDER BY Timestamp DESC;
GO
```

**Option C: Visual Studio**
1. Open the solution in Visual Studio
2. View -> SQL Server Object Explorer
3. Add SQL Server -> Server: `localhost,1433`, Auth: SQL Server Authentication, User: `sa`, Password: `YourStrong!Passw0rd`

### Adding New Migrations

If you modify the `TransactionDbContext` or `ComplianceDbContext` models:

```bash
cd backend

# Install EF tools (first time only)
dotnet tool restore

# Generate a migration for Transaction Service
dotnet ef migrations add YourMigrationName \
  --project src/FinancialPlatform.TransactionService \
  --output-dir Data/Migrations

# Generate a migration for Compliance Service
dotnet ef migrations add YourMigrationName \
  --project src/FinancialPlatform.ComplianceService \
  --output-dir Data/Migrations
```

---

## Testing

### Backend Tests (xUnit)

```bash
cd backend

# Run all tests (unit + integration)
dotnet test

# Run only unit tests (108 tests)
dotnet test tests/FinancialPlatform.UnitTests

# Run only integration tests (19 tests)
dotnet test tests/FinancialPlatform.IntegrationTests

# Run with verbose output
dotnet test -v normal
```

**Test categories:**

| Category | Count | What's tested |
|----------|-------|--------------|
| Unit - AdaptiveEventBus | 14 | Bus starts, publishes, subscribes, switches backends, config-based constructor, backward compatibility |
| Unit - Distributed Backends | 18 | Redis/RabbitMQ/Kafka construction, subscription, publish-without-start, empty-connection handling |
| Unit - EventSerializer | 5 | JSON serialization, deserialization, round-trip across all event types |
| Unit - EventTypeResolver | 5 | Type name resolution for all 4 event types, unknown type rejection |
| Unit - EventBusConnectionConfig | 3 | Default values, positional parameters, record equality |
| Unit - InMemoryEventBus | 3 | Event delivery, multiple handlers, no-handler case |
| Unit - InMemorySseHub | 7 | Client add/remove, broadcast, connection counting |
| Unit - TransactionService | 13 | CRUD operations, validation, event publishing |
| Unit - AuditService | 13 | Hash chain creation, integrity, SHA-256 correctness |
| Unit - RiskEvaluation | 8 | Score calculation, velocity detection, odd hours, multiple flags |
| Unit - PaymentGateway | 9 | Authorization rules, amount limits, risk thresholds, boundary conditions |
| Unit - PinBlockService | 9 | PIN block encrypt/decrypt round-trips, Format 0 layout, PAN block construction, KCV, translation, validation |
| Unit - JwtService | 6 | Token generation, claim validation, expiry |
| Integration - API Gateway | 9 | Login, register, health, auth middleware, duplicate users |
| Integration - Transaction HTTP | 7 | Create, get, list, validation errors |
| Integration - Event Flow | 3 | Full pipeline: create -> risk -> payment -> audit |
| Integration - PinEncryptionService | 10 | HSM health, key management, PIN encrypt/decrypt/verify, ISO 8583 parse/build/authorize |

**Total: 159 backend tests.**

### Frontend Tests (Vitest)

```bash
cd frontend/apps/transaction-ui && npm test    # Component, store, and API tests
cd frontend/apps/admin-dashboard && npm test   # App component tests
cd frontend/apps/risk-dashboard && npm test    # App component tests
cd frontend/apps/audit-dashboard && npm test   # App component tests
cd frontend/apps/card-operations && npm test   # HSM, PIN, and ISO 8583 component tests
```

---

## Running in Visual Studio

1. Open `backend/FinancialPlatform.slnx` in Visual Studio 2022 (v17.14 or later)
2. In Solution Explorer, right-click the solution -> **Configure Startup Projects**
3. Select **Multiple startup projects** and set these to **Start**:
   - `FinancialPlatform.ApiGateway`
   - `FinancialPlatform.TransactionService`
   - `FinancialPlatform.RiskService`
   - `FinancialPlatform.PaymentService`
   - `FinancialPlatform.ComplianceService`
   - `FinancialPlatform.PinEncryptionService`
4. Press **F5** (or Ctrl+F5 without debugging)

All 6 services will start simultaneously. The Output window shows logs from each service.

A `.slnlaunch` file is included - Visual Studio should detect it and offer the "All Microservices" launch profile automatically.

**Note:** SQL Server must be running before starting the services. Either start it from Docker (`docker-compose up sqlserver`) or use a local installation.

---

## Project Structure

```
adaptive-realtime-financial-transaction-platform/
├── backend/
│   ├── FinancialPlatform.slnx              # Visual Studio solution file
│   ├── FinancialPlatform.slnlaunch         # Multi-startup project configuration
│   ├── .config/dotnet-tools.json           # Local .NET tools (dotnet-ef)
│   ├── src/
│   │   ├── FinancialPlatform.Shared/       # Models, DTOs, events, interfaces, enums
│   │   ├── FinancialPlatform.EventInfrastructure/
│   │   │   ├── Bus/                        # InMemory, Redis, RabbitMQ, Kafka, Adaptive event bus
│   │   │   ├── Configuration/              # EventBusConnectionConfig record
│   │   │   ├── Serialization/              # EventEnvelope, EventSerializer, EventTypeResolver
│   │   │   └── Sse/                        # InMemorySseHub for SSE broadcast
│   │   ├── FinancialPlatform.ApiGateway/   # JWT auth, SSE, routing (port 5000)
│   │   ├── FinancialPlatform.TransactionService/  # Transaction CRUD + SQL (port 5001)
│   │   ├── FinancialPlatform.RiskService/  # Risk scoring engine (port 5002)
│   │   ├── FinancialPlatform.PaymentService/       # Payment authorization (port 5003)
│   │   ├── FinancialPlatform.ComplianceService/    # Audit logging + SQL (port 5004)
│   │   └── FinancialPlatform.PinEncryptionService/ # HSM + ISO 8583 + PIN verification (port 5005)
│   └── tests/
│       ├── FinancialPlatform.UnitTests/            # 130 unit tests
│       └── FinancialPlatform.IntegrationTests/     # 29 integration tests
├── frontend/
│   └── apps/
│       ├── transaction-ui/                 # Transaction submission + KPI dashboard (3001)
│       ├── admin-dashboard/                # Service health monitoring (3002)
│       ├── risk-dashboard/                 # Risk scoring visualization (3003)
│       ├── audit-dashboard/                # Audit trail + hash chain (3004)
│       └── card-operations/                # HSM keys, PIN ops, ISO 8583 tools (3005)
├── infrastructure/
│   ├── docker-compose.yml                  # Full platform stack
│   ├── prometheus/prometheus.yml           # Prometheus scrape config
│   └── grafana/                            # Grafana provisioning + dashboards
├── postman/
│   ├── FinancialPlatform.postman_collection.json  # API collection with 25+ requests
│   ├── FinancialPlatform-Docker.postman_environment.json  # Docker environment
│   └── FinancialPlatform-Local.postman_environment.json   # Local environment
├── database/
│   └── seeds/seed_data.json                # Initial user seed data
├── design-document.md                      # Full system design specification
└── CLAUDE.md                               # Development reference
```

---

## Environment Variables

### API Gateway

| Variable | Default | Description |
|----------|---------|-------------|
| `ASPNETCORE_ENVIRONMENT` | Production | Runtime environment |
| `Jwt__SecretKey` | (none) | JWT signing key (32+ chars) |
| `Jwt__Issuer` | FinancialPlatform | JWT issuer claim |
| `ServiceUrls__TransactionService` | http://localhost:5001 | Transaction service URL |
| `ServiceUrls__RiskService` | http://localhost:5002 | Risk service URL |
| `ServiceUrls__PaymentService` | http://localhost:5003 | Payment service URL |
| `ServiceUrls__ComplianceService` | http://localhost:5004 | Compliance service URL |
| `ServiceUrls__PinEncryptionService` | http://localhost:5005 | PIN Encryption service URL |

### Backend Services (Transaction, Risk, Payment, Compliance)

| Variable | Default | Description |
|----------|---------|-------------|
| `EventBus__DefaultBackend` | InMemory | Event bus backend: InMemory, Redis, RabbitMQ, or Kafka |
| `EventBus__RedisUrl` | localhost:6379 | Redis connection URL |
| `EventBus__RabbitMqUrl` | amqp://guest:guest@localhost:5672 | RabbitMQ AMQP URL |
| `EventBus__KafkaBrokers` | localhost:9092 | Kafka broker addresses |
| `EventBus__ServiceName` | (varies) | Service name for consumer group |

### Database Services Only (Transaction, Compliance)

| Variable | Default | Description |
|----------|---------|-------------|
| `ConnectionStrings__DefaultConnection` | (varies) | SQL Server connection string |

---

## Troubleshooting

### "Cannot connect to SQL Server"

- Ensure SQL Server is running: `docker-compose up sqlserver`
- Wait ~30 seconds for it to be ready (check: `docker-compose logs sqlserver`)
- Verify the port is free: `netstat -an | grep 1433`

### Port already in use

```bash
# Windows - find what's using a port
netstat -ano | findstr :5000

# Kill the process (replace PID)
taskkill /PID 12345 /F
```

### Database migrations fail

Migrations are applied automatically. If they fail:
1. Delete the database: connect via SSMS and drop `FinancialPlatform_Transactions` and `FinancialPlatform_Compliance`
2. Restart the service - it will recreate the database from scratch

### Docker build fails

```bash
# Force a clean rebuild (no cache)
docker-compose build --no-cache

# Check Docker disk space
docker system df
docker system prune    # Free unused space
```

### Frontend npm install fails

```bash
# Clear npm cache
npm cache clean --force
rm -rf node_modules package-lock.json
npm install
```

### Events not flowing across services in Docker

- Check that Redis is running: `docker-compose logs redis`
- Verify `EventBus__DefaultBackend=Redis` is set in docker-compose.yml for all backend services
- Check that `EventBus__RedisUrl=redis:6379` uses the Docker service name (not `localhost`)

### Windows Smart App Control blocks test DLLs

If integration tests fail with "Application Control policy has blocked this file":
1. Open Windows Security -> App & Browser Control -> Smart App Control
2. Set to **Off** (or add the project folder to exclusions)
3. Clean and rebuild: `dotnet clean && dotnet build`
