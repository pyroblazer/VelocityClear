#!/usr/bin/env bash
set -e
echo "=== Stopping previous containers ==="
docker compose -f infrastructure/docker-compose.yml down -v --remove-orphans 2>/dev/null || true

echo "=== Building and starting all services ==="
docker compose -f infrastructure/docker-compose.yml up --build "$@"
