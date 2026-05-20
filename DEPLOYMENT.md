# VelocityClear Free Deployment Guide

This guide walks you through deploying the entire VelocityClear platform using free-tier SaaS services.

## Architecture Overview

```
Vercel (free)                        MonsterASP.net (free)
┌──────────────────────┐            ┌─────────────────────────────────┐
│  Superapp (shell)    │            │  FinancialPlatform.AllServices  │
│  + Integration Demo  │──/api/*──▶│  (all 6 microservices in 1 app)│
│  + Microfrontends    │            │                                 │
│                      │            │  SQL Server DB (1 database)    │
└──────────────────────┘            └───────┬──────────┬──────────────┘
                                            │          │
               ┌────────────────────────────┘          │
               │                                       │
    External SaaS (Event Bus)              External SaaS (Monitoring)
    ┌─────────────────────────┐            ┌─────────────────────────┐
    │ Upstash Redis           │            │ Grafana Cloud           │
    │ CloudAMQP RabbitMQ      │            │  ├─ Prometheus (metrics)│
    │ Aiven Kafka (+ keepalive)│           │  └─ Grafana (dashboards)│
    └─────────────────────────┘            └─────────────────────────┘
```

## Prerequisites

| Account | Free Tier | URL |
|---------|-----------|-----|
| MonsterASP.net | 1 site, 256 MB RAM, 1 SQL Server DB, 1 GB storage | https://monsterasp.net |
| Vercel | Unlimited sites, 100 GB bandwidth | https://vercel.com |
| Upstash | 500K Redis commands/month, 256 MB | https://upstash.com |
| CloudAMQP | 1M messages/month, 20 connections (Little Lemur) | https://cloudamqp.com |
| Aiven | Kafka free tier | https://aiven.io |
| Grafana Labs | 10K active series, 13-month retention | https://grafana.com |

---

## Step 1: Deploy the Backend on MonsterASP.net

### 1a. Create a new site

1. Log in to [MonsterASP.net](https://monsterasp.net)
2. Go to **Hosting** → **Create Site**
3. Choose **ASP.NET Core** runtime
4. Note your site URL (e.g., `https://velocityclear.runasp.net`)

### 1b. Get your SQL Server connection string

1. Go to your site → **Databases**
2. Create a new SQL Server database
3. Copy the connection string
4. Set this as `ConnectionStrings__DefaultConnection` in your environment variables

### 1c. Configure environment variables

In MonsterASP.net, go to your site → **Environment Variables** and set:

| Variable | Value | Source |
|----------|-------|--------|
| `ASPNETCORE_ENVIRONMENT` | `Production` | - |
| `ConnectionStrings__DefaultConnection` | Your MonsterASP.net SQL Server connection string | MonsterASP.net control panel |
| `Jwt__SecretKey` | A 32+ character secret key | Generate: `openssl rand -base64 48` |
| `Jwt__Issuer` | `FinancialPlatform` | - |
| `EventBus__DefaultBackend` | `InMemory` (or `RabbitMQ`, `Redis`, `Kafka`) | Your choice |
| `EventBus__ServiceName` | `velocityclear-platform` | - |
| `EventBus__RedisUrl` | See Upstash section below | Upstash console |
| `EventBus__RabbitMqUrl` | See CloudAMQP section below | CloudAMQP console |
| `EventBus__KafkaBrokers` | See Aiven section below | Aiven console |
| `ServiceUrls__TransactionService` | `http://localhost:5000` | All services point to self |
| `ServiceUrls__RiskService` | `http://localhost:5000` | Same |
| `ServiceUrls__PaymentService` | `http://localhost:5000` | Same |
| `ServiceUrls__ComplianceService` | `http://localhost:5000` | Same |
| `ServiceUrls__PinEncryptionService` | `http://localhost:5000` | Same |

### 1d. Publish and deploy

```bash
cd backend/src/FinancialPlatform.AllServices
dotnet publish -c Release -o ./publish
```

Upload the contents of `./publish/` to MonsterASP.net via their deployment tool or FTP.

---

## Step 2: Configure Upstash Redis

### 2a. Create a Redis database

1. Log in to [Upstash Console](https://console.upstash.com)
2. Click **Create Database**
3. Name: `velocityclear`
4. Region: Choose closest to MonsterASP.net
5. Click **Create**

### 2b. Get the connection string

1. Open your Redis database → **Details**
2. Copy the **REST URL** and **REST Token** (for reference)
3. For the .NET `StackExchange.Redis` connection string, use:
   ```
   desired-silkworm-41673.upstash.io:6379,password=<YOUR_REST_TOKEN>,ssl=True
   ```
4. Set this as `EventBus__RedisUrl` in your MonsterASP.net environment variables

### 2c. Test the connection

```bash
# Using redis-cli
redis-cli -u redis://default:<TOKEN>@desired-silkworm-41673.upstash.io:6379 ping
# Expected: PONG
```

To use Redis as the event bus, set:
```
EventBus__DefaultBackend=Redis
EventBus__RedisUrl=desired-silkworm-41673.upstash.io:6379,password=<TOKEN>,ssl=True
```

---

## Step 3: Configure CloudAMQP RabbitMQ

### 3a. Your instance details

Your RabbitMQ instance is already set up:

| Setting | Value |
|---------|-------|
| Host | `mouse.rmq5.cloudamqp.com` |
| User/Vhost | `wjgmnkqh` |
| Port | 5672 (5671 for TLS) |
| Management UI | https://customer.cloudamqp.com |

### 3b. Set the connection URL

```
EventBus__RabbitMqUrl=amqps://wjgmnkqh:<YOUR_PASSWORD>@mouse.rmq5.cloudamqp.com/wjgmnkqh
```

Replace `<YOUR_PASSWORD>` with your CloudAMQP password (from the instance details page).

### 3c. To use RabbitMQ as the event bus

```
EventBus__DefaultBackend=RabbitMQ
EventBus__RabbitMqUrl=amqps://wjgmnkqh:<PASSWORD>@mouse.rmq5.cloudamqp.com/wjgmnkqh
```

### 3d. Monitor

Visit the CloudAMQP dashboard to see message rates, connections, and queue depths.

---

## Step 4: Configure Aiven Kafka

### 4a. Your instance details

| Setting | Value |
|---------|-------|
| Broker | `chatapp-react-next-kafka-chatapp-react-next.h.aivencloud.com:25204` |
| Security | SSL |
| Client ID | `velocityclear-backend` |
| Group ID | `velocityclear-consumer-group` |

### 4b. Set the connection URL

```
EventBus__KafkaBrokers=chatapp-react-next-kafka-chatapp-react-next.h.aivencloud.com:25204
```

### 4c. Keep-alive (automatic)

The `KafkaKeepAliveService` in the consolidated backend automatically sends a ping message to the `__health-check` topic every 5 minutes. This prevents Aiven from shutting down the free Kafka cluster due to inactivity.

To use Kafka as the event bus:
```
EventBus__DefaultBackend=Kafka
EventBus__KafkaBrokers=chatapp-react-next-kafka-chatapp-react-next.h.aivencloud.com:25204
```

---

## Step 5: Configure Grafana Cloud Monitoring

### 5a. Set up Prometheus scraping (Metrics Endpoint)

1. Log in to [Grafana Cloud](https://pyroblazerv2.grafana.net)
2. Go to **Home → Connections → Add new connection**
3. Search for **"Metrics Endpoint"**
4. Configure the scrape job:
   - **Scrape Job URL**: `https://velocityclear.runasp.net/metrics`
   - **Authentication**: Bearer or Basic (set credentials if your `/metrics` endpoint requires auth)
   - **Scrape Interval**: 60 seconds
5. Click **Test Connection** — you should see successful metrics collection
6. Click **Save Scrape Job**

### 5b. Import the VelocityClear dashboard

1. In Grafana, go to **Dashboards → Import**
2. Upload the JSON file from `infrastructure/grafana/dashboards/financial-platform.json`
3. Select the Prometheus data source (your Grafana Cloud instance)
4. Click **Import**

### 5c. Alternative: remote_write with Grafana Alloy

If you want to push metrics instead of letting Grafana pull them:

1. Install [Grafana Alloy](https://grafana.com/docs/alloy/latest/) on your infrastructure
2. Add to your Alloy config:

```hcl
prometheus.scrape "default" {
  targets = [{"__address__" = "localhost:5000"}]
  forward_to = [prometheus.remote_write.grafanacloud.receiver]
  scrape_interval = "15s"
}

prometheus.remote_write "grafanacloud" {
  endpoint {
    url = "https://prometheus-prod-52-prod-ap-southeast-2.grafana.net/api/prom/push"
    basic_auth {
      username = "3224592"
      password = "<YOUR_GRAFANA_API_TOKEN>"
    }
  }
}
```

Generate your API token at: Grafana Cloud → **Profile → API Keys**

---

## Step 6: Deploy the Superapp on Vercel

### 6a. Push to GitHub

Make sure your code is on a GitHub repository.

### 6b. Import in Vercel

1. Go to [Vercel Dashboard](https://vercel.com/new)
2. Import your GitHub repository
3. Configure the project:
   - **Root Directory**: `frontend/apps/superapp`
   - **Framework Preset**: Vite
   - **Build Command**: `npm run build`
   - **Output Directory**: `dist`
4. Set environment variables (none needed for the frontend itself)
5. Click **Deploy**

### 6c. Configure API proxying

Edit `frontend/apps/superapp/vercel.json` and replace `YOUR_BACKEND_URL` with your MonsterASP.net site URL:

```json
"destination": "https://velocityclear.runasp.net/api/hsm/:path*"
```

Then redeploy on Vercel.

### 6d. Set up microfrontend deployments (optional)

Each standalone frontend can be deployed to Vercel separately:

1. Import the same repo, but set different root directories:
   - `frontend/apps/admin-dashboard`
   - `frontend/apps/risk-dashboard`
   - `frontend/apps/audit-dashboard`
   - `frontend/apps/card-operations`
   - `frontend/apps/transaction-ui`
   - `frontend/apps/integration-demo`
2. Each will get its own URL (e.g., `admin-dashboard.vercel.app`)
3. The Superapp's Module Federation config references these URLs

---

## Step 7: Run EF Core Migrations

The consolidated app auto-migrates on startup (all 3 DbContexts). If you need to run migrations manually:

```bash
cd backend/src/FinancialPlatform.AllServices

# Generate migrations (first time only, if starting fresh)
dotnet ef migrations add InitialCreate \
  --project ../FinancialPlatform.ApiGateway \
  --startup-project . \
  --context GatewayDbContext

dotnet ef migrations add InitialCreate \
  --project ../FinancialPlatform.TransactionService \
  --startup-project . \
  --context TransactionDbContext

dotnet ef migrations add InitialCreate \
  --project ../FinancialPlatform.ComplianceService \
  --startup-project . \
  --context ComplianceDbContext
```

---

## API Documentation (Swagger)

All backend services expose interactive Swagger UI for exploring and testing API endpoints.

### Accessing Swagger UI

**Docker Compose (local development):**

| Service | URL |
|---------|-----|
| API Gateway | http://localhost:5000/swagger |
| Transaction Service | http://localhost:5001/swagger |
| Risk Service | http://localhost:5002/swagger |
| Payment Service | http://localhost:5003/swagger |
| Compliance Service | http://localhost:5004/swagger |
| PIN Encryption Service | http://localhost:5005/swagger |

**MonsterASP.net (production):**

Open `https://velocityclear.runasp.net/swagger` — the consolidated `AllServices` project serves Swagger for all endpoints under a single UI.

### What you can do

- Browse all available endpoints grouped by controller
- See request/response schemas and example payloads
- Try endpoints directly from the browser (click **Try it out**)
- Download the OpenAPI spec at `/swagger/v1/swagger.json`

---

## Event Bus Backend Reference

The `AdaptiveEventBus` switches between backends based on CPU load. You can pin a specific backend via the `EventBus__DefaultBackend` env var:

| Value | Use Case | External Service |
|-------|----------|-----------------|
| `InMemory` | Single process, most efficient | None needed |
| `Redis` | Low-latency, moderate throughput | Upstash |
| `RabbitMQ` | Reliable delivery, good throughput | CloudAMQP |
| `Kafka` | High throughput, event streaming | Aiven |

For the consolidated deployment on MonsterASP.net, `InMemory` is recommended (everything runs in one process). Switch to `RabbitMQ` or `Redis` to demonstrate the adaptive event bus switching.

---

## Step 8: Automated Deployment (GitHub Actions CI/CD)

A GitHub Actions workflow automatically deploys the backend to MonsterASP.net when you push to `main`.

### 8a. Configure GitHub Secrets

You only need to add one secret to your GitHub repo:

Go to **Settings** → **Secrets and variables** → **Actions** and add:

| Secret | Value |
|--------|-------|
| `MONSTERASP_SERVER_PASSWORD` | Your WebDeploy password (from MonsterASP.net control panel → Deploy → WebDeploy) |

The server name (`site69774.siteasp.net`), site name (`site69774`), and username (`site69774`) are already configured in the workflow.

### 8c. Workflow details

The workflow (`.github/workflows/deploy-monsterasp.yml`) triggers on:
- Push to `main` when files under `backend/` change
- Manual trigger via **Actions** tab → **Deploy to MonsterASP.net** → **Run workflow**

It runs on `windows-latest`, publishes with `--runtime win-x86`, and deploys via WebDeploy.

---

## Step 9: Terminal Deployment (Manual WebDeploy)

If you need to deploy from your local terminal without Git:

### 9a. Set up credentials

Create `.env.deploy` in the project root (**never commit this file** — it's in `.gitignore`):

```bash
MONSTERASP_PASSWORD=your-webdeploy-password
```

The server and username are pre-configured in the deploy scripts. Only the password needs to be set.

### 9b. Run the deploy script

**Windows (recommended — uses WebDeploy directly):**

```cmd
scripts\deploy-monsterasp.bat
```

**Git Bash / WSL:**

```bash
./scripts/deploy-monsterasp.sh           # build + deploy
./scripts/deploy-monsterasp.sh --skip-build  # deploy only
```

Requires `msdeploy.exe` (installed at `C:\Program Files (x86)\IIS\Microsoft Web Deploy V3\` with Visual Studio or [Web Deploy 3.6+](https://www.iis.net/downloads/microsoft/web-deploy)).

### 9c. Alternative: Manual FTP deployment

1. Stop the site in MonsterASP.net control panel
2. Connect via FTP (credentials in MonsterASP.net → Deploy → FTP)
3. Upload contents of `backend/src/FinancialPlatform.AllServices/publish/` to `/wwwroot/`
4. Start the site

---

## Troubleshooting

### Backend won't start on MonsterASP.net
- Check that `ConnectionStrings__DefaultConnection` is set correctly
- Verify `Jwt__SecretKey` is at least 32 characters
- Check MonsterASP.net logs in the control panel
- Check `stdout` logs at `/logs/stdout_*.log` on the server

### API calls from Vercel return CORS errors
- MonsterASP.net should automatically handle CORS for ASP.NET Core
- If not, add CORS policy in `Program.cs`:
  ```csharp
  builder.Services.AddCors(options => {
      options.AddDefaultPolicy(policy => {
          policy.WithOrigins("https://your-superapp.vercel.app", "https://velocityclear.runasp.net")
                .AllowAnyHeader().AllowAnyMethod();
      });
  });
  app.UseCors();
  ```

### Grafana Cloud shows no metrics
- Verify the Metrics Endpoint scrape job URL is correct
- Check that your MonsterASP.net site's `/metrics` endpoint returns data:
  `curl https://velocityclear.runasp.net/metrics`

### Aiven Kafka shuts down
- The `KafkaKeepAliveService` sends pings every 5 minutes
- If still shutting down, reduce the interval in `KafkaKeepAliveService.cs`

### CloudAMQP connection fails
- Verify the password is correct
- Check that you're using `amqps://` (with 's') not `amqp://`
- Ensure you have under 20 concurrent connections
