# Test Seed Data

This document describes the users and transactions loaded by the seed command so you can immediately explore and test every part of the platform.

## Running the Seed

### Docker (recommended)

The `db-seed` container runs automatically when you bring the full stack up. It waits for EF Core migrations to finish and then resets both Users and Transactions tables.

```bash
cd infrastructure

# First time — build everything and seed runs automatically
docker-compose up --build

# Re-seed at any time while the stack is running
docker-compose run --rm db-seed
```

### Local (SQL Server already running)

```bash
# Start only SQL Server if not using full Docker
cd infrastructure && docker-compose up sqlserver

# Then apply EF migrations (creates the DB and tables)
cd backend
dotnet run --project src/FinancialPlatform.TransactionService   # starts and migrates on launch, Ctrl+C after ~5s

# Run the seed script
/opt/mssql-tools18/bin/sqlcmd \
  -S localhost,1433 -U sa -P "YourStrong!Passw0rd" -C \
  -i database/seed.sql
```

---

## Seeded Users

These four accounts are available immediately after seeding. Use them to log in at the Superapp (`http://localhost:3000`) or via the API (`POST /api/auth/login`).

| Username   | Password     | Role    | What they can do |
|------------|--------------|---------|------------------|
| `admin`    | `admin123`   | Admin   | Create transactions, view everything, access all dashboards |
| `trader1`  | `trader123`  | User    | Create transactions, view their own data |
| `auditor1` | `auditor123` | Auditor | Read-only access to audit logs and compliance data |
| `testuser` | `test123`    | User    | Create transactions, useful for beginner testing |

**Note:** The platform uses plain-text password comparison in this demo. In production, passwords would be bcrypt-hashed.

---

## Seeded Transactions

22 transactions are pre-loaded covering every status, currency, and risk level.

### Status Reference

| Status      | Integer | Meaning |
|-------------|---------|---------|
| Pending     | 0       | Just created, awaiting processing |
| Approved    | 1       | Passed risk check, approved for payment |
| Rejected    | 2       | Denied — failed risk or amount limit |
| HighRisk    | 3       | Flagged by RiskService (risk score ≥ 50) |
| Processing  | 4       | In-flight through the payment pipeline |
| Completed   | 5       | Fully settled |
| Failed      | 6       | Error during processing |

### Transaction List

#### admin (a0000000-0000-0000-0000-000000000001)

| ID        | Amount       | Currency | Status    | Description               | Counterparty          |
|-----------|--------------|----------|-----------|---------------------------|-----------------------|
| txn-0001  | $500.00      | USD      | Completed | Office supplies           | Staples Corp          |
| txn-0002  | $1,200.00    | USD      | Completed | Software license renewal  | JetBrains             |
| txn-0003  | $75,000.00   | USD      | Rejected  | Equipment purchase        | TechCo Ltd            |
| txn-0004  | €3,500.00    | EUR      | Completed | Conference registration   | DevConf GmbH          |
| txn-0005  | $15,000.00   | USD      | HighRisk  | Night-time wire transfer  | Acme Finance          |

- **txn-0003** is rejected because the amount ($75 000) exceeds the $50 000 hard limit.
- **txn-0005** is HighRisk because it's > $10 000 (+50 risk) and was submitted at 23:10 UTC (+15 risk) → score 65 (MEDIUM, but stored as HighRisk).

#### trader1 (a0000000-0000-0000-0000-000000000002)

| ID        | Amount       | Currency | Status    | Description               | Counterparty          |
|-----------|--------------|----------|-----------|---------------------------|-----------------------|
| txn-0006  | $250.50      | USD      | Completed | Trading fee               | NYSE Gateway          |
| txn-0007  | $8,500.00    | USD      | Approved  | Equity purchase           | Fidelity Investments  |
| txn-0008  | $12,000.00   | USD      | HighRisk  | Options contract          | CBOE Exchange         |
| txn-0009  | £999.99      | GBP      | Completed | FX conversion             | Barclays FX Desk      |
| txn-0010  | $4,750.00    | USD      | Completed | Dividend reinvestment     | Vanguard Fund         |
| txn-0011  | $55,000.00   | USD      | Rejected  | Large block trade         | Goldman Sachs         |
| txn-0012  | €2,100.00    | EUR      | Completed | ETF purchase              | Deutsche Bank         |
| txn-0013  | $150.00      | USD      | Completed | Commission rebate         | Interactive Brokers   |

- **txn-0011** is rejected — over $50 000 limit.
- **txn-0008** is HighRisk — amount > $10 000.

#### auditor1 (a0000000-0000-0000-0000-000000000003)

| ID        | Amount       | Currency | Status    | Description               | Counterparty          |
|-----------|--------------|----------|-----------|---------------------------|-----------------------|
| txn-0014  | $100.00      | USD      | Completed | Audit test — low amount   | Internal Testing      |
| txn-0015  | $6,000.00    | USD      | Approved  | Audit test — medium       | Internal Testing      |
| txn-0016  | $85,000.00   | USD      | Rejected  | Audit test — over limit   | Internal Testing      |

These three transactions demonstrate each risk band for audit-trail verification.

#### testuser (a0000000-0000-0000-0000-000000000004)

| ID        | Amount       | Currency | Status    | Description               | Counterparty          |
|-----------|--------------|----------|-----------|---------------------------|-----------------------|
| txn-0017  | $49.99       | USD      | Completed | Monthly subscription      | Netflix               |
| txn-0018  | $1,500.00    | USD      | Completed | Rent payment              | City Rentals LLC      |
| txn-0019  | €320.75      | EUR      | Completed | Online shopping           | Amazon EU             |
| txn-0020  | $5,500.00    | USD      | HighRisk  | Home repair payment       | BuildRight Contractors|
| txn-0021  | $200.00      | USD      | Pending   | Transfer in progress      | Personal Savings      |
| txn-0022  | $11,000.00   | USD      | Failed    | Wire — processing error   | Overseas Bank         |

- **txn-0021** stays Pending — useful for testing the live SSE stream (submit a new transaction to see it transition).
- **txn-0022** demonstrates the Failed state.

---

## Test Scenarios by Dashboard

### Transactions (http://localhost:3000/transactions)

1. **Log in as `trader1` / `trader123`** — should see txn-0006 through txn-0013 in the table.
2. **Log in as `admin` / `admin123`** — admin sees all 22 transactions.
3. **Submit a new transaction** using the form: amount `500`, currency `USD`, description `Test`. Watch the SSE stream add it to the table in real time.
4. **Submit a high-risk transaction**: amount `90000` — it should come back Rejected immediately (exceeds $50 000 limit).
5. **Submit a borderline transaction**: amount `6000`, currency `USD` — RiskService flags it as medium risk (+30 points), PaymentService approves it (risk < 80 and amount ≤ $50 000 with risk < 50 for the $5 000 + risk ≥ 50 rule… actually risk is 30 so approved).

### Admin Dashboard (http://localhost:3000/admin)

1. All six service health cards are visible (Transaction :5001, Risk :5002, Payment :5003, Compliance :5004, PIN Encryption :5005, API Gateway :5000).
2. **Check system health** button triggers a health poll across all services.
3. The KPI counter for total transactions should reflect the 22 seeded records once the TransactionService responds.

### Risk Monitor (http://localhost:3000/risk)

1. The risk distribution chart shows how many transactions fall in each risk band.
2. Submit rapid transactions (> 5 within 1 minute) as the same user to trigger the velocity rule (+25 risk points) and watch the HIGH risk count rise.
3. Submit a transaction at a local time between 22:00 and 06:00 UTC to trigger the night-time rule (+15 points).

### Audit Trail (http://localhost:3000/audit)

1. The hash-chain timeline shows all events recorded by ComplianceService. Events appear here only after the live event pipeline has processed them (seed data does not retroactively create audit entries — create a new transaction to see an entry appear).
2. Use the **Event type filter** to show only `TransactionCreated` events.
3. Use the **search box** to find entries by transaction ID (e.g., `txn-0001`).
4. Click **Verify chain** to confirm the SHA-256 chain is intact (all green = no tampering).

### Card Operations (http://localhost:3000/cards)

1. **HSM Health** — confirm the software HSM is running and reports key count.
2. **Generate / list keys** via the Key Management tab.
3. **Encrypt a PIN**: enter PIN `1234`, PAN `4111111111111111`, key index `0`. Note the returned PIN block.
4. **Translate a PIN block** between key zones.
5. **Build an ISO 8583 authorization message** using the form and verify the response bitmap.

---

## Resetting to a Clean State

To wipe all data and re-seed from scratch:

```bash
cd infrastructure
docker-compose down -v              # removes containers AND named volumes (DB data)
docker-compose up --build           # rebuilds, migrates, and seeds automatically
```

Or to keep the stack running and just re-seed:

```bash
docker-compose run --rm db-seed
```
