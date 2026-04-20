#!/bin/bash
# seed.sh — wait for EF Core migrations to finish, then run seed.sql
#
# Called by the db-seed service in docker-compose.yml.
# Uses restart: on-failure so Docker retries until this script exits 0.

SQLCMD="/opt/mssql-tools18/bin/sqlcmd"
HOST="sqlserver"
PORT="1433"
# SA_PASSWORD is injected by docker-compose from the .env file — never hardcode here
SA_PASS="${SA_PASSWORD:?SA_PASSWORD env var must be set}"
CONN="-S $HOST,$PORT -U sa -P $SA_PASS -C"

echo "[seed] Waiting for SQL Server to accept connections..."
until $SQLCMD $CONN -Q "SELECT 1" &>/dev/null; do
  echo "[seed] SQL Server not ready yet, retrying in 5s..."
  sleep 5
done

echo "[seed] Waiting for FinancialPlatform_Transactions DB and Users table..."
until $SQLCMD $CONN -Q \
  "IF EXISTS (SELECT 1 FROM FinancialPlatform_Transactions.INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME='Users') SELECT 1 ELSE SELECT 0" \
  2>/dev/null | grep -q "1"; do
  echo "[seed] Tables not ready yet (EF migrations pending), retrying in 5s..."
  sleep 5
done

echo "[seed] Running seed.sql..."
$SQLCMD $CONN -i /database/seed.sql

echo "[seed] Done."
