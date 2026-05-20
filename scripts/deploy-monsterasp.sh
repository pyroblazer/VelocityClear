#!/usr/bin/env bash
# Deploy FinancialPlatform.AllServices to MonsterASP.net via WebDeploy
#
# Prerequisites:
#   1. Create .env.deploy with your credentials (see .env.deploy.example)
#   2. Run: dotnet publish backend/src/FinancialPlatform.AllServices \
#             --configuration Release --runtime win-x86 \
#             --output backend/src/FinancialPlatform.AllServices/publish
#
# Usage:
#   ./scripts/deploy-monsterasp.sh           # build + deploy
#   ./scripts/deploy-monsterasp.sh --skip-build  # deploy only

set -euo pipefail
cd "$(dirname "$0")/.."

# Load .env.deploy if it exists
if [ -f .env.deploy ]; then
  set -a; source .env.deploy; set +a
fi

WEBSITE_NAME="${MONSTERASP_WEBSITE_NAME:-site69774}"
SERVER_URL="${MONSTERASP_SERVER_URL:-https://site69774.siteasp.net:8172/msdeploy.axd?site=site69774}"
USERNAME="${MONSTERASP_USERNAME:-site69774}"
PASSWORD="${MONSTERASP_PASSWORD:?Set MONSTERASP_PASSWORD in .env.deploy}"

PUBLISH_DIR="backend/src/FinancialPlatform.AllServices/publish"
MSDEPLOY="/c/Program Files (x86)/IIS/Microsoft Web Deploy V3/msdeploy.exe"

if [ "${1:-}" != "--skip-build" ]; then
  echo "Publishing (win-x86)..."
  dotnet publish backend/src/FinancialPlatform.AllServices \
    --configuration Release --runtime win-x86 --output "$PUBLISH_DIR"
fi

if [ ! -d "$PUBLISH_DIR" ]; then
  echo "Error: $PUBLISH_DIR not found. Run without --skip-build first."
  exit 1
fi

echo "Deploying to MonsterASP.net..."
echo "  Site: $WEBSITE_NAME"
echo "  Server: $SERVER_URL"

"$MSDEPLOY" \
  -source:contentPath="$PUBLISH_DIR" \
  -dest:contentPath="$WEBSITE_NAME",computerName="$SERVER_URL",userName="$USERNAME",password="$PASSWORD",authtype="Basic" \
  -verb:sync \
  -allowUntrusted \
  -enableRule:DoNotDeleteRule \
  -enableRule:AppOffline

echo "Deploy complete."
