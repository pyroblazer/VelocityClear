# Backup Procedures

**Platform:** VelocityClear Adaptive Real-Time Financial Transaction Platform
**Last Reviewed:** 2026-04-22
**Owner:** Operations & SRE Team

---

## Overview

This document defines the backup and restore procedures for the VelocityClear platform's persistent data stores. It covers SQL Server databases, Docker volumes, and configuration state. The procedures support the platform's Recovery Point Objective (RPO) of less than 5 minutes and Recovery Time Objective (RTO) of less than 1 hour.

---

## Databases

The platform uses two SQL Server databases:

| Database | Service | Tables | Purpose |
|----------|---------|--------|---------|
| `FinancialPlatform_Transactions` | TransactionService | Transactions, Users | Transaction records and user accounts |
| `FinancialPlatform_Compliance` | ComplianceService | AuditLogs | Tamper-evident audit trail with SHA-256 hash chain |

Both databases run in a single SQL Server Docker container with data persisted to a Docker named volume.

---

## Method 1: Docker Volume Backup

The SQL Server data is stored in a Docker named volume. This method captures the entire SQL Server data directory, including all databases, transaction logs, and server-level configuration.

### Prerequisites

- Docker CLI access on the host
- Sufficient disk space for the backup archive (typically 500MB - 2GB)
- The SQL Server container name is `sqlserver` (or `financial_sqlserver` depending on the Docker Compose project name)

### Create a Backup

```bash
# Step 1: Stop the SQL Server container to ensure data consistency
# This is required for a crash-consistent backup
docker-compose -f infrastructure/docker-compose.yml stop sqlserver

# Step 2: Verify the container is stopped
docker-compose -f infrastructure/docker-compose.yml ps sqlserver

# Step 3: Create a tar archive of the volume
# Replace "sqlserver_data" with the actual volume name if different
docker run --rm \
  -v sqlserver_data:/data \
  -v $(pwd):/backup \
  alpine \
  tar czf /backup/sqlserver_backup_$(date +%Y%m%d_%H%M%S).tar.gz -C /data .

# Step 4: Verify the backup was created
ls -lh sqlserver_backup_*.tar.gz

# Step 5: Restart SQL Server
docker-compose -f infrastructure/docker-compose.yml start sqlserver

# Step 6: Wait for SQL Server to be ready
until docker exec sqlserver /opt/mssql-tools/bin/sqlcmd -S localhost -U sa -P "$SA_PASSWORD" -Q "SELECT 1" > /dev/null 2>&1; do
  echo "Waiting for SQL Server to start..."
  sleep 5
done
echo "SQL Server is ready."
```

### Restore from Volume Backup

```bash
# Step 1: Stop SQL Server
docker-compose -f infrastructure/docker-compose.yml stop sqlserver

# Step 2: Back up the current volume (in case the restore fails)
docker run --rm \
  -v sqlserver_data:/data \
  -v $(pwd):/backup \
  alpine \
  tar czf /backup/sqlserver_pre_restore_$(date +%Y%m%d_%H%M%S).tar.gz -C /data .

# Step 3: Clear the volume and extract the backup
# Replace YYYYMMDD_HHMMSS with the actual timestamp from the backup filename
docker run --rm \
  -v sqlserver_data:/data \
  -v $(pwd):/backup \
  alpine \
  sh -c "rm -rf /data/* && tar xzf /backup/sqlserver_backup_YYYYMMDD_HHMMSS.tar.gz -C /data"

# Step 4: Restart SQL Server
docker-compose -f infrastructure/docker-compose.yml start sqlserver

# Step 5: Wait for SQL Server to be ready
until docker exec sqlserver /opt/mssql-tools/bin/sqlcmd -S localhost -U sa -P "$SA_PASSWORD" -Q "SELECT 1" > /dev/null 2>&1; do
  echo "Waiting for SQL Server to start..."
  sleep 5
done

# Step 6: Verify databases are accessible
docker exec sqlserver /opt/mssql-tools/bin/sqlcmd -S localhost -U sa -P "$SA_PASSWORD" \
  -Q "SELECT name, state_desc FROM sys.databases"

# Step 7: Verify audit chain integrity
curl -s http://localhost:5004/api/audit/verify | jq .
```

---

## Method 2: SQL Server Native Backup (sqlcmd)

This method uses SQL Server's native BACKUP DATABASE command to create .bak files. This is the recommended method for production backups because it does not require stopping the SQL Server container and supports point-in-time recovery with transaction log backups.

### Prerequisites

- A backup directory must exist inside the SQL Server container
- SQL Server service account must have write access to the backup directory

### Setup (One-Time)

```bash
# Create the backup directory inside the container
docker exec sqlserver mkdir -p /var/opt/mssql/backup
```

### Create Full Database Backups

```bash
# Back up the Transactions database
docker exec sqlserver /opt/mssql-tools/bin/sqlcmd -S localhost -U sa -P "$SA_PASSWORD" \
  -Q "BACKUP DATABASE [FinancialPlatform_Transactions] TO DISK='/var/opt/mssql/backup/transactions_full.bak' WITH FORMAT, INIT, NAME='Transactions-Full Backup', COMPRESSION, STATS=10"

# Back up the Compliance database
docker exec sqlserver /opt/mssql-tools/bin/sqlcmd -S localhost -U sa -P "$SA_PASSWORD" \
  -Q "BACKUP DATABASE [FinancialPlatform_Compliance] TO DISK='/var/opt/mssql/backup/compliance_full.bak' WITH FORMAT, INIT, NAME='Compliance-Full Backup', COMPRESSION, STATS=10"

# Verify the backup files exist
docker exec sqlserver ls -lh /var/opt/mssql/backup/
```

### Create Transaction Log Backups

Transaction log backups enable point-in-time recovery and are required to meet the RPO of less than 5 minutes.

```bash
# Back up the Transactions database transaction log
docker exec sqlserver /opt/mssql-tools/bin/sqlcmd -S localhost -U sa -P "$SA_PASSWORD" \
  -Q "BACKUP LOG [FinancialPlatform_Transactions] TO DISK='/var/opt/mssql/backup/transactions_log.trn' WITH FORMAT, INIT, NAME='Transactions-Log Backup', COMPRESSION"

# Back up the Compliance database transaction log
docker exec sqlserver /opt/mssql-tools/bin/sqlcmd -S localhost -U sa -P "$SA_PASSWORD" \
  -Q "BACKUP LOG [FinancialPlatform_Compliance] TO DISK='/var/opt/mssql/backup/compliance_log.trn' WITH FORMAT, INIT, NAME='Compliance-Log Backup', COMPRESSION"
```

### Copy Backup Files to Host

```bash
# Copy backup files from the container to the host
docker cp sqlserver:/var/opt/mssql/backup/transactions_full.bak ./backup/
docker cp sqlserver:/var/opt/mssql/backup/compliance_full.bak ./backup/
docker cp sqlserver:/var/opt/mssql/backup/transactions_log.trn ./backup/
docker cp sqlserver:/var/opt/mssql/backup/compliance_log.trn ./backup/
```

### Restore from Full Backup

```bash
# Step 1: Restore the Transactions database
docker exec sqlserver /opt/mssql-tools/bin/sqlcmd -S localhost -U sa -P "$SA_PASSWORD" \
  -Q "RESTORE DATABASE [FinancialPlatform_Transactions] FROM DISK='/var/opt/mssql/backup/transactions_full.bak' WITH REPLACE, RECOVERY"

# Step 2: Restore the Compliance database
docker exec sqlserver /opt/mssql-tools/bin/sqlcmd -S localhost -U sa -P "$SA_PASSWORD" \
  -Q "RESTORE DATABASE [FinancialPlatform_Compliance] FROM DISK='/var/opt/mssql/backup/compliance_full.bak' WITH REPLACE, RECOVERY"

# Step 3: Verify databases are online
docker exec sqlserver /opt/mssql-tools/bin/sqlcmd -S localhost -U sa -P "$SA_PASSWORD" \
  -Q "SELECT name, state_desc FROM sys.databases"
```

### Restore with Point-in-Time Recovery

```bash
# Step 1: Restore the full backup with NORECOVERY (leaves the database in restoring state)
docker exec sqlserver /opt/mssql-tools/bin/sqlcmd -S localhost -U sa -P "$SA_PASSWORD" \
  -Q "RESTORE DATABASE [FinancialPlatform_Transactions] FROM DISK='/var/opt/mssql/backup/transactions_full.bak' WITH REPLACE, NORECOVERY"

# Step 2: Apply the transaction log backup with STOPAT for point-in-time recovery
# Replace the datetime with the desired recovery point
docker exec sqlserver /opt/mssql-tools/bin/sqlcmd -S localhost -U sa -P "$SA_PASSWORD" \
  -Q "RESTORE LOG [FinancialPlatform_Transactions] FROM DISK='/var/opt/mssql/backup/transactions_log.trn' WITH RECOVERY, STOPAT='2026-04-22T14:30:00'"

# Step 3: Verify the database is online
docker exec sqlserver /opt/mssql-tools/bin/sqlcmd -S localhost -U sa -P "$SA_PASSWORD" \
  -Q "SELECT name, state_desc FROM sys.databases WHERE name='FinancialPlatform_Transactions'"
```

---

## Method 3: Data Export via sqlcmd

For lightweight backups or data migration, individual tables can be exported as text files.

### Export Data to CSV

```bash
# Export Transactions table
docker exec sqlserver /opt/mssql-tools/bin/sqlcmd -S localhost -U sa -P "$SA_PASSWORD" \
  -d FinancialPlatform_Transactions \
  -Q "SELECT * FROM Transactions" \
  -s "," -W > transactions_export_$(date +%Y%m%d).csv

# Export Users table
docker exec sqlserver /opt/mssql-tools/bin/sqlcmd -S localhost -U sa -P "$SA_PASSWORD" \
  -d FinancialPlatform_Transactions \
  -Q "SELECT * FROM Users" \
  -s "," -W > users_export_$(date +%Y%m%d).csv

# Export AuditLogs table
docker exec sqlserver /opt/mssql-tools/bin/sqlcmd -S localhost -U sa -P "$SA_PASSWORD" \
  -d FinancialPlatform_Compliance \
  -Q "SELECT * FROM AuditLogs" \
  -s "," -W > auditlogs_export_$(date +%Y%m%d).csv
```

### Import Data from CSV

```bash
# Import data using BULK INSERT (requires the CSV file to be accessible inside the container)
# First, copy the CSV into the container
docker cp transactions_export_20260422.csv sqlserver:/var/opt/mssql/backup/

# Then, import using BULK INSERT
docker exec sqlserver /opt/mssql-tools/bin/sqlcmd -S localhost -U sa -P "$SA_PASSWORD" \
  -d FinancialPlatform_Transactions \
  -Q "BULK INSERT Transactions FROM '/var/opt/mssql/backup/transactions_export_20260422.csv' WITH (FIELDTERMINATOR=',', ROWTERMINATOR='\n', FIRSTROW=2)"
```

---

## Method 4: Database Seeding

To reset the database to a known state with test data, use the seed utility.

### Reset and Re-Seed

```bash
# Run the db-seed service from Docker Compose
# This clears existing data and repopulates with test data from database/seeds/
docker-compose -f infrastructure/docker-compose.yml run --rm db-seed
```

The seed utility runs `database/seed.sql` which:
1. Clears the Users and Transactions tables in `FinancialPlatform_Transactions`
2. Repopulates with the test dataset documented in `SEED_DATA.md`
3. Ensures the admin user (`admin` / `admin123`) is always available

**Warning:** Seeding is a destructive operation. All existing transaction and user data will be lost. The compliance audit log is not affected by seeding.

---

## Post-Restore Verification

After any restore operation, perform the following verification steps:

### 1. Database Connectivity

```bash
docker exec sqlserver /opt/mssql-tools/bin/sqlcmd -S localhost -U sa -P "$SA_PASSWORD" \
  -Q "SELECT name, state_desc FROM sys.databases"
```

Expected output: Both databases should show `ONLINE` state.

### 2. Table Row Counts

```bash
docker exec sqlserver /opt/mssql-tools/bin/sqlcmd -S localhost -U sa -P "$SA_PASSWORD" \
  -Q "USE FinancialPlatform_Transactions; SELECT 'Transactions' as TableName, COUNT(*) as RowCount FROM Transactions UNION ALL SELECT 'Users', COUNT(*) FROM Users"

docker exec sqlserver /opt/mssql-tools/bin/sqlcmd -S localhost -U sa -P "$SA_PASSWORD" \
  -Q "USE FinancialPlatform_Compliance; SELECT 'AuditLogs' as TableName, COUNT(*) as RowCount FROM AuditLogs"
```

Verify that row counts match the expected values from before the incident.

### 3. Audit Chain Integrity

```bash
curl -s http://localhost:5004/api/audit/verify | jq .
```

Expected output: `{"valid": true, "totalEntries": <count>, "verifiedEntries": <count>}`

If the chain is broken, the response will indicate which entries are affected. Investigate any chain failure as a potential P1 security incident.

### 4. Service Health

```bash
for port in 5000 5001 5002 5003 5004 5005; do
  echo "Port $port: $(curl -s -o /dev/null -w '%{http_code}' http://localhost:$port/health)"
done
```

All services should return HTTP 200.

### 5. End-to-End Transaction Test

```bash
# Obtain a JWT token
TOKEN=$(curl -s -X POST http://localhost:5000/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{"username":"admin","password":"admin123"}' | jq -r '.token')

# Create a test transaction
curl -s -X POST http://localhost:5000/api/transactions \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{"userId":"admin","amount":100.00,"currency":"USD","type":"Transfer"}' | jq .

# Verify the audit log captured the transaction events
curl -s http://localhost:5004/api/audit | jq '. | length'
```

---

## Backup Schedule

### Development Environment

| Backup Type | Frequency | Retention | Method |
|-------------|-----------|-----------|--------|
| Manual volume backup | Before destructive testing | Until next backup | Method 1 |
| Re-seed | As needed | N/A | Method 4 |

### Staging Environment

| Backup Type | Frequency | Retention | Method |
|-------------|-----------|-----------|--------|
| Full database backup | Daily at 02:00 UTC | 7 days | Method 2 |
| Volume snapshot | Weekly at 03:00 UTC | 4 weeks | Method 1 |

### Production Environment

| Backup Type | Frequency | Retention | Method |
|-------------|-----------|-----------|--------|
| Full database backup | Every 4 hours | 30 days | Method 2 |
| Transaction log backup | Every 5 minutes | 7 days | Method 2 |
| Volume snapshot | Every 6 hours | 14 days | Method 1 |
| Offsite copy | Daily | 90 days | Copy to remote storage |

---

## Automated Backup Script

For production deployments, use the following cron schedule to automate backups:

```bash
#!/bin/bash
# backup.sh - Automated SQL Server backup for VelocityClear

BACKUP_DIR="/var/backups/velocityclear"
TIMESTAMP=$(date +%Y%m%d_%H%M%S)
SA_PASSWORD="${SA_PASSWORD}"

# Ensure backup directory exists
mkdir -p "$BACKUP_DIR"

# Full backup
docker exec sqlserver /opt/mssql-tools/bin/sqlcmd -S localhost -U sa -P "$SA_PASSWORD" \
  -Q "BACKUP DATABASE [FinancialPlatform_Transactions] TO DISK='/var/opt/mssql/backup/transactions_full_${TIMESTAMP}.bak' WITH FORMAT, INIT, COMPRESSION, STATS=10"

docker exec sqlserver /opt/mssql-tools/bin/sqlcmd -S localhost -U sa -P "$SA_PASSWORD" \
  -Q "BACKUP DATABASE [FinancialPlatform_Compliance] TO DISK='/var/opt/mssql/backup/compliance_full_${TIMESTAMP}.bak' WITH FORMAT, INIT, COMPRESSION, STATS=10"

# Copy to host
docker cp "sqlserver:/var/opt/mssql/backup/transactions_full_${TIMESTAMP}.bak" "$BACKUP_DIR/"
docker cp "sqlserver:/var/opt/mssql/backup/compliance_full_${TIMESTAMP}.bak" "$BACKUP_DIR/"

# Clean up backups older than retention period (30 days)
find "$BACKUP_DIR" -name "*.bak" -mtime +30 -delete

echo "Backup completed at $(date)"
```

### Cron Configuration

```cron
# Full backup every 4 hours
0 */4 * * * /usr/local/bin/backup.sh >> /var/log/velocityclear-backup.log 2>&1

# Transaction log backup every 5 minutes
*/5 * * * * /usr/local/bin/tlog-backup.sh >> /var/log/velocityclear-backup.log 2>&1
```

---

## Disaster Recovery

### Total System Loss

In the event of total system loss (host failure, data center outage):

1. Provision a new host with Docker and Docker Compose installed
2. Clone the platform repository: `git clone <repository-url>`
3. Copy the latest backup files to the new host
4. Configure environment variables in `.env` (from `.env.example`)
5. Start SQL Server: `docker-compose up -d sqlserver`
6. Wait for SQL Server to be ready
7. Copy backup files into the container: `docker cp <backup-file> sqlserver:/var/opt/mssql/backup/`
8. Restore databases following Method 2 restore procedure
9. Start all services: `docker-compose up -d`
10. Perform post-restore verification

**Estimated Recovery Time:** 30-60 minutes depending on data size and network speed.

---

## Change Log

| Date | Version | Author | Description |
|------|---------|--------|-------------|
| 2026-04-22 | 1.0 | Operations & SRE Team | Initial backup procedures documentation |
