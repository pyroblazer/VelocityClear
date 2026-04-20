# Alerting Runbook

**Platform:** VelocityClear Adaptive Real-Time Financial Transaction Platform
**Last Reviewed:** 2026-04-22
**Owner:** Operations & SRE Team

---

## Overview

This runbook provides step-by-step procedures for responding to each Prometheus alert configured for the VelocityClear platform. For each alert, it describes the symptoms, probable causes, diagnostic steps, and resolution procedures.

**Reference:** Prometheus alerting rules are defined in `infrastructure/prometheus.yml`

---

## General Response Steps

For any alert, follow this general workflow:

1. **Acknowledge** the alert within the target time for the assessed severity
2. **Assess** the severity based on the incident response criteria
3. **Diagnose** using the specific diagnostic steps in this runbook
4. **Resolve** using the resolution steps for the specific alert
5. **Verify** that all health checks pass and no further alerts are firing
6. **Document** the incident and any actions taken

---

## Alert: ServiceDown

**Severity:** P1 (Critical)

**Description:** A backend microservice health endpoint has returned a non-200 status or has been unreachable for more than 60 seconds.

**Alert Expression:**
```promql
up{job=~"financial-platform-.*"} == 0
```

**Symptoms:**
- One or more services showing as `DOWN` in Prometheus targets
- Health endpoint (`/health`) returning 5xx or timing out
- Users unable to access the affected service's functionality
- Dependent services may show cascading failures

**Probable Causes:**
1. Container crash (OOM kill, unhandled exception)
2. Database connection failure causing health check to fail
3. Docker daemon issue on the host
4. Network partition within the Docker network
5. Startup failure due to configuration error

**Diagnostic Steps:**

```bash
# Step 1: Check which service is down
docker-compose ps

# Step 2: Check container logs for the affected service
docker-compose logs --tail=100 <service-name>

# Step 3: Check if the container is running but unhealthy
docker inspect <container-name> | grep -A 10 "Health"

# Step 4: Check database connectivity
docker exec -it sqlserver /opt/mssql-tools/bin/sqlcmd -S localhost -U sa -P "$SA_PASSWORD" -Q "SELECT 1"

# Step 5: Check Docker daemon health
docker info

# Step 6: Check system resources
docker stats --no-stream
```

**Resolution Steps:**

**If the container has exited:**
```bash
# Restart the specific service
docker-compose up -d <service-name>

# Monitor logs during startup
docker-compose logs -f <service-name>
```

**If the container is running but unhealthy:**
```bash
# Check for OOM kills
dmesg | grep -i "oom"

# If OOM, increase memory limits in docker-compose.yml
# Restart the service
docker-compose restart <service-name>
```

**If database connectivity is lost:**
```bash
# Restart SQL Server first
docker-compose restart sqlserver

# Wait for SQL Server to be ready (check logs)
docker-compose logs -f sqlserver

# Then restart dependent services
docker-compose restart transaction-service compliance-service
```

**If Docker daemon issues:**
```bash
# Restart Docker daemon
sudo systemctl restart docker

# Restart all services
docker-compose up -d
```

**Verification:**
```bash
# Check all targets in Prometheus
curl -s http://localhost:9090/api/v1/targets | jq '.data.activeTargets[] | {labels: .labels.job, health: .health}'

# Test the affected service health endpoint
curl http://localhost:<port>/health
```

---

## Alert: HighErrorRate

**Severity:** P2 (High)

**Description:** The rate of HTTP 5xx responses across the platform exceeds the acceptable threshold for more than 5 minutes.

**Alert Expression:**
```promql
rate(http_requests_total{status=~"5.."}[5m]) / rate(http_requests_total[5m]) > 0.05
```

**Symptoms:**
- Users receiving 500 Internal Server Error responses
- Transaction failures or incomplete processing
- Elevated error count in Grafana dashboards
- Serilog logs showing exception stack traces

**Probable Causes:**
1. Database connection pool exhaustion
2. Unhandled exceptions in application code
3. Invalid or corrupted data causing processing failures
4. Resource exhaustion (CPU, memory, disk)
5. Dependency failure (Redis, RabbitMQ, Kafka)

**Diagnostic Steps:**

```bash
# Step 1: Identify which endpoints are returning errors
curl -s 'http://localhost:9090/api/v1/query?query=rate(http_requests_total{status=~"5.."}[5m])' | jq '.data.result[] | {path: .metric.path, status: .metric.status, value: .value}'

# Step 2: Check service logs for exceptions
docker-compose logs --tail=200 <service-name> | grep -i "error\|exception\|fail"

# Step 3: Check database connection pool
docker exec -it sqlserver /opt/mssql-tools/bin/sqlcmd -S localhost -U sa -P "$SA_PASSWORD" -Q "SELECT COUNT(*) FROM sys.dm_exec_connections"

# Step 4: Check system resources
docker stats --no-stream
df -h  # Disk usage
```

**Resolution Steps:**

**For database connection pool exhaustion:**
```bash
# Check for long-running queries
docker exec -it sqlserver /opt/mssql-tools/bin/sqlcmd -S localhost -U sa -P "$SA_PASSWORD" -Q "SELECT session_id, status, command, wait_type, wait_time FROM sys.dm_exec_requests WHERE status = 'running'"

# Restart the affected services to reset connection pools
docker-compose restart <service-name>
```

**For resource exhaustion:**
```bash
# Increase resource limits in docker-compose.yml
# Restart affected services
docker-compose up -d <service-name>
```

**For dependency failures:**
```bash
# Restart the failed dependency
docker-compose restart redis

# Then restart services that depend on it
docker-compose restart apigateway transaction-service risk-service payment-service compliance-service pin-encryption-service
```

**Verification:**
```bash
# Check error rate has returned to normal
curl -s 'http://localhost:9090/api/v1/query?query=rate(http_requests_total{status=~"5.."}[5m]) / rate(http_requests_total[5m])' | jq '.data.result'
```

---

## Alert: HighLatencyP95

**Severity:** P3 (Medium) / P2 if persistent

**Description:** The 95th percentile response latency for API endpoints exceeds the 500ms SLA threshold for more than 5 minutes.

**Alert Expression:**
```promql
histogram_quantile(0.95, rate(http_request_duration_seconds_bucket[5m])) > 0.5
```

**Symptoms:**
- Slow page loads in frontend applications
- Transaction submissions taking longer than expected
- Users reporting unresponsive application
- Elevated latency on Grafana dashboards

**Probable Causes:**
1. Database query performance degradation (missing indexes, table scan)
2. Increased transaction volume exceeding capacity
3. Event bus backlog causing processing delays
4. Garbage collection pressure in .NET runtime
5. Docker host resource contention

**Diagnostic Steps:**

```bash
# Step 1: Identify which endpoints are slow
curl -s 'http://localhost:9090/api/v1/query?query=histogram_quantile(0.95, rate(http_request_duration_seconds_bucket[5m]))' | jq '.data.result[] | {path: .metric.path, p95: .value}'

# Step 2: Check database query performance
docker exec -it sqlserver /opt/mssql-tools/bin/sqlcmd -S localhost -U sa -P "$SA_PASSWORD" -Q "SELECT TOP 10 total_elapsed_time/1000 as elapsed_ms, command, wait_type FROM sys.dm_exec_requests ORDER BY total_elapsed_time DESC"

# Step 3: Check transaction volume
curl -s 'http://localhost:9090/api/v1/query?query=rate(http_requests_total{method="POST",path="/api/transactions"}[5m])' | jq '.data.result'

# Step 4: Check system resources
docker stats --no-stream
```

**Resolution Steps:**

**For database performance:**
```bash
# Check for blocking processes
docker exec -it sqlserver /opt/mssql-tools/bin/sqlcmd -S localhost -U sa -P "$SA_PASSWORD" -Q "SELECT blocking_session_id, session_id, wait_type, wait_time FROM sys.dm_exec_requests WHERE blocking_session_id <> 0"

# Update statistics for query optimization
docker exec -it sqlserver /opt/mssql-tools/bin/sqlcmd -S localhost -U sa -P "$SA_PASSWORD" -Q "UPDATE STATISTICS Transactions"
docker exec -it sqlserver /opt/mssql-tools/bin/sqlcmd -S localhost -U sa -P "$SA_PASSWORD" -Q "UPDATE STATISTICS AuditLogs"
```

**For high transaction volume:**
- Scale up the affected services (increase Docker resource limits)
- Enable Redis caching for frequently queried data
- Consider horizontal scaling for the API Gateway

**For event bus backlog:**
```bash
# Check Redis stream length (if using Redis event bus)
docker exec -it redis redis-cli XLEN transactions_stream

# Check consumer group lag
docker exec -it redis redis-cli XINFO GROUPS transactions_stream
```

**Verification:**
```bash
# Check P95 latency has returned below 500ms
curl -s 'http://localhost:9090/api/v1/query?query=histogram_quantile(0.95, rate(http_request_duration_seconds_bucket[5m]))' | jq '.data.result'
```

---

## Alert: JwtAuthFailureSpike

**Severity:** P2 (High) - Potential security incident

**Description:** The rate of JWT authentication failures has spiked significantly above the baseline, indicating a possible brute-force attack or credential compromise.

**Alert Expression:**
```promql
rate(http_requests_total{status="401"}[5m]) > 10
```

**Symptoms:**
- High volume of 401 Unauthorized responses
- Login endpoint receiving unusual traffic
- Logs showing repeated failed authentication attempts
- Potential credential stuffing attack pattern

**Probable Causes:**
1. Brute-force attack on the login endpoint
2. Compromised JWT signing key
3. Expired or misconfigured JWT tokens
4. Misconfigured client sending invalid tokens
5. Application bug in token generation or validation

**Diagnostic Steps:**

```bash
# Step 1: Check the volume of 401 responses by endpoint
curl -s 'http://localhost:9090/api/v1/query?query=rate(http_requests_total{status="401"}[5m])' | jq '.data.result[] | {path: .metric.path, rate: .value}'

# Step 2: Check API Gateway logs for authentication failures
docker-compose logs --tail=500 apigateway | grep -i "401\|unauthorized\|authentication"

# Step 3: Check if the failures are from specific IP addresses
docker-compose logs --tail=500 apigateway | grep "401" | awk '{print $1}' | sort | uniq -c | sort -rn | head -20

# Step 4: Check if JWT signing key is still valid
docker-compose exec apigateway printenv | grep Jwt__SecretKey
```

**Resolution Steps:**

**For brute-force attack:**
```bash
# Verify rate limiting is active
curl -s 'http://localhost:9090/api/v1/query?query=rate(http_requests_total{status="429"}[5m])' | jq

# If rate limiting is not catching the attack, block the offending IPs at the infrastructure level
# Consider temporarily reducing the login rate limit
```

**For compromised JWT signing key:**
```bash
# Immediately rotate the JWT signing key (see docs/security/secrets-management.md)
# Generate a new key
openssl rand -base64 32

# Update the environment variable and restart the API Gateway
docker-compose down apigateway
# Update Jwt__SecretKey in docker-compose.yml or .env
docker-compose up -d apigateway
```

**For misconfigured clients:**
- Identify the client application from the user agent in logs
- Notify the client team of the authentication issue
- Verify token format and claims

**Verification:**
```bash
# Check that 401 response rate has returned to baseline
curl -s 'http://localhost:9090/api/v1/query?query=rate(http_requests_total{status="401"}[5m])' | jq '.data.result'
```

**Security Escalation:**
If a brute-force attack or credential compromise is confirmed, escalate to the Security and Compliance Team immediately. Follow the incident response procedure for a potential P1 security incident.

---

## Alert: SseConnectionLimit

**Severity:** P3 (Medium)

**Description:** The number of active Server-Sent Events (SSE) connections is approaching or has reached the platform limit, which may cause new clients to be unable to receive real-time updates.

**Alert Expression:**
```promql
sse_connections_active > 900
```

**Symptoms:**
- New frontend clients unable to establish SSE connections
- Real-time updates not flowing to some dashboards
- Users need to refresh to see new data
- Warning messages in API Gateway logs about connection limits

**Probable Causes:**
1. Legitimate increase in concurrent users exceeding capacity
2. Clients not properly closing SSE connections (connection leak)
3. Browser tabs left open accumulating connections
4. Automated monitoring tools opening connections without closing

**Diagnostic Steps:**

```bash
# Step 1: Check current SSE connection count
curl -s 'http://localhost:9090/api/v1/query?query=sse_connections_active' | jq '.data.result'

# Step 2: Check API Gateway logs for connection events
docker-compose logs --tail=200 apigateway | grep -i "sse\|connection"

# Step 3: Check for connection leaks (connections from same source)
docker-compose logs --tail=500 apigateway | grep "SSE connected" | awk '{print $NF}' | sort | uniq -c | sort -rn | head -20
```

**Resolution Steps:**

**For legitimate traffic increase:**
```bash
# Increase the SSE connection limit in the API Gateway configuration
# Update the SseHub or Program.cs configuration
# Restart the API Gateway
docker-compose restart apigateway
```

**For connection leaks:**
```bash
# Implement server-side connection timeout for idle SSE connections
# Ensure clients send heartbeat messages and disconnect stale connections
# Restart the API Gateway to reset all connections
docker-compose restart apigateway
```

**Verification:**
```bash
# Check SSE connection count has decreased
curl -s 'http://localhost:9090/api/v1/query?query=sse_connections_active' | jq '.data.result'
```

---

## Alert: HighRiskTransactionRate

**Severity:** P3 (Medium) / P2 if sustained

**Description:** The rate of transactions classified as HIGH risk exceeds the normal baseline, potentially indicating fraudulent activity or a change in transaction patterns.

**Alert Expression:**
```promql
rate(risk_evaluations_total{level="HIGH"}[15m]) / rate(risk_evaluations_total[15m]) > 0.2
```

**Symptoms:**
- High percentage of transactions receiving risk scores >= 80
- Increased payment rejections (HIGH risk transactions with amount > $5K)
- Risk dashboard showing elevated risk levels
- Compliance team flagging unusual patterns

**Probable Causes:**
1. Coordinated fraud attack on the platform
2. Legitimate seasonal increase in high-value transactions
3. Risk scoring model producing false positives (rule changes)
4. Test data or load test transactions skewing metrics
5. Velocity detection triggered by legitimate user behavior (batch transactions)

**Diagnostic Steps:**

```bash
# Step 1: Check risk score distribution
curl -s 'http://localhost:9090/api/v1/query?query=rate(risk_evaluations_total[15m])' | jq '.data.result[] | {level: .metric.level, rate: .value}'

# Step 2: Check which users are generating high-risk transactions
docker-compose logs --tail=500 risk-service | grep -i "HIGH\|score.*80\|score.*90"

# Step 3: Check transaction amounts for recent high-risk transactions
docker-compose logs --tail=500 transaction-service | grep -i "amount" | tail -50

# Step 4: Check if velocity rules are triggering
docker-compose logs --tail=200 risk-service | grep -i "velocity\|rate"
```

**Resolution Steps:**

**For fraud attack:**
```bash
# Identify affected user accounts
# Block suspicious accounts (requires admin action)
# Escalate to Security and Compliance Team
# Consider temporarily tightening risk scoring thresholds
```

**For false positives from velocity rules:**
```bash
# Review the velocity detection threshold (5 transactions/minute)
# Adjust the threshold if legitimate batch processing is triggering it
# Restart the RiskService if configuration changes are needed
docker-compose restart risk-service
```

**For test data:**
```bash
# Identify and filter test transactions from metrics
# Re-seed the database with realistic test data if needed
docker-compose run --rm db-seed
```

**Verification:**
```bash
# Check HIGH risk transaction rate has returned to baseline
curl -s 'http://localhost:9090/api/v1/query?query=rate(risk_evaluations_total{level="HIGH"}[15m]) / rate(risk_evaluations_total[15m])' | jq '.data.result'
```

---

## Alert: PaymentRejectionSpike

**Severity:** P2 (High)

**Description:** The rate of payment rejections has spiked above the normal baseline, indicating a systemic issue with payment authorization.

**Alert Expression:**
```promql
rate(payment_authorizations_total{result="rejected"}[5m]) / rate(payment_authorizations_total[5m]) > 0.3
```

**Symptoms:**
- High percentage of transactions being rejected by the PaymentService
- Users reporting failed transactions
- Payment dashboard showing elevated rejection count
- Revenue impact from lost transactions

**Probable Causes:**
1. RiskService returning inflated risk scores (cascading from HighRiskTransactionRate)
2. PIN verification failures for card transactions (PinEncryptionService issue)
3. Payment authorization rules triggering (amount > $50K, risk >= 80, amount > $5K AND risk >= 50)
4. Configuration change affecting payment rules
5. HSM key issue causing PIN decryption failures

**Diagnostic Steps:**

```bash
# Step 1: Check rejection reasons
docker-compose logs --tail=500 payment-service | grep -i "reject" | tail -50

# Step 2: Check risk scores for recent transactions
curl -s 'http://localhost:9090/api/v1/query?query=rate(risk_evaluations_total[5m])' | jq '.data.result[] | {level: .metric.level, rate: .value}'

# Step 3: Check PIN verification status
docker-compose logs --tail=200 pin-encryption-service | grep -i "verify\|pin\|error"

# Step 4: Check HSM health
curl http://localhost:5005/api/hsm/health

# Step 5: Check payment service logs for authorization decisions
docker-compose logs --tail=500 payment-service | grep -i "authorized\|reject\|amount\|risk"
```

**Resolution Steps:**

**For cascading risk scoring issues:**
```bash
# Resolve the underlying risk scoring issue first (see HighRiskTransactionRate runbook)
# Once risk scores normalize, payment rejections should decrease
```

**For PIN verification failures:**
```bash
# Check HSM key status
curl http://localhost:5005/api/hsm/keys

# If the LMK has changed, restart the PIN Encryption Service with the correct key
docker-compose restart pin-encryption-service

# Verify PIN encryption round-trip
curl -X POST http://localhost:5005/api/hsm/pin/encrypt \
  -H "Content-Type: application/json" \
  -d '{"pan":"4111111111111111","pin":"1234"}'
```

**For payment rule misconfiguration:**
```bash
# Review the authorization rules in PaymentService
# Ensure rules match the business requirements documented in CLAUDE.md
# If rules were inadvertently changed, revert and restart
docker-compose restart payment-service
```

**Verification:**
```bash
# Check rejection rate has returned to baseline
curl -s 'http://localhost:9090/api/v1/query?query=rate(payment_authorizations_total{result="rejected"}[5m]) / rate(payment_authorizations_total[5m])' | jq '.data.result'

# Process a test transaction
curl -X POST http://localhost:5000/api/transactions \
  -H "Authorization: Bearer <token>" \
  -H "Content-Type: application/json" \
  -d '{"userId":"test-user","amount":100.00,"currency":"USD","type":"Transfer"}'
```

---

## General Troubleshooting Commands

### Service Health Check

```bash
# Check all services
for port in 5000 5001 5002 5003 5004 5005; do
  echo "Port $port: $(curl -s -o /dev/null -w '%{http_code}' http://localhost:$port/health)"
done
```

### Docker Compose Status

```bash
# Show status of all containers
docker-compose ps

# Show resource usage
docker stats --no-stream
```

### Log Aggregation

```bash
# Follow all service logs
docker-compose logs -f --tail=50

# Follow specific service logs
docker-compose logs -f --tail=100 <service-name>

# Search logs across all services
docker-compose logs --tail=1000 | grep -i "<search-term>"
```

### Database Queries

```bash
# Check database connectivity
docker exec -it sqlserver /opt/mssql-tools/bin/sqlcmd -S localhost -U sa -P "$SA_PASSWORD" -Q "SELECT name, state_desc FROM sys.databases"

# Check transaction count
docker exec -it sqlserver /opt/mssql-tools/bin/sqlcmd -S localhost -U sa -P "$SA_PASSWORD" -Q "USE FinancialPlatform_Transactions; SELECT COUNT(*) FROM Transactions"

# Check audit log count
docker exec -it sqlserver /opt/mssql-tools/bin/sqlcmd -S localhost -U sa -P "$SA_PASSWORD" -Q "USE FinancialPlatform_Compliance; SELECT COUNT(*) FROM AuditLogs"
```

---

## Change Log

| Date | Version | Author | Description |
|------|---------|--------|-------------|
| 2026-04-22 | 1.0 | Operations & SRE Team | Initial alerting runbook |
