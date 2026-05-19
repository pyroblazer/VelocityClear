# VelocityClear Local Setup Guide

Everything runs locally with no Docker needed. Two terminals: one for the backend, one for the frontend.

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- [Node.js 18+](https://nodejs.org/)
- SQL Server is remote (MonsterASP.net) — no local DB needed

## Quick Start

### Terminal 1 — Backend

```bash
cd backend/src/FinancialPlatform.AllServices
dotnet run --urls http://localhost:5000
```

Wait until you see:

```
[INF] All databases migrated successfully.
[INF] Now listening on: http://localhost:5000
```

### Terminal 2 — Frontend (Superapp)

```bash
cd frontend/apps/superapp
npm install
npm run dev
```

Open **http://localhost:3000** in your browser.

## Login Credentials

| Username   | Password     | Role    |
|------------|--------------|---------|
| `admin`    | `admin123`   | Admin   |
| `trader1`  | `trader123`  | User    |
| `auditor1` | `auditor123` | Auditor |
| `testuser` | `test1234`   | User    |

## What's Running

| Service | URL |
|---------|-----|
| Superapp UI | http://localhost:3000 |
| Swagger API Docs | http://localhost:5000/swagger |
| OpenAPI JSON | http://localhost:5000/swagger/v1/swagger.json |
| Prometheus Metrics | http://localhost:5000/metrics |

All 6 microservices (API Gateway, Transaction, Risk, Payment, Compliance, PIN Encryption) are consolidated into a single process on port 5000.

## Troubleshooting

### "A network-related error occurred" on startup

The SQL Server connection string in `appsettings.json` points to the MonsterASP.net remote database. Check your internet connection. The database must be reachable from your machine.

### Port 5000 already in use

```bash
# Find and kill the process
lsof -i :5000    # macOS/Linux
netstat -ano | findstr :5000   # Windows
```

Or use a different port:

```bash
dotnet run --urls http://localhost:5001
```

Then update the frontend proxy in `frontend/apps/superapp/vite.config.ts` to match.

### Port 3000 already in use

Vite will auto-increment to 3001, 3002, etc. Check the terminal output for the actual URL.

### Swagger returns 401 Unauthorized

The API uses an API key middleware. To bypass it for local development, check the `ApiKeyMiddleware` configuration. Swagger UI should work without auth on `/swagger`.

## Architecture (Local)

```
Browser (:3000)  ──proxy──▶  AllServices (:5000)
    │                             │
    │  Vite dev server            │  Single .NET process
    │  proxies /api/*             │  All 6 microservices
    │                             │  + SQL Server (remote)
    └─────────────────────────────┘
```
