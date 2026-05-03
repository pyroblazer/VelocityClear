@echo off
echo === Stopping previous containers ===
docker compose -f infrastructure/docker-compose.yml down -v --remove-orphans 2>nul

echo === Building and starting all services ===
docker compose -f infrastructure/docker-compose.yml up --build %*
