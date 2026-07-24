#!/usr/bin/env bash
#
# setup-db.sh — provision a fresh StudioTechBI test database end-to-end.
#
# Why this script exists: real schema state here is NOT purely migration-driven.
# EF Core migration files apply first, but several recent tables/columns
# (ReportDesignerConsents, SchemaModels, SchemaModelFields,
# SchemaModelFieldAliases, AiBoundaryAuditEvents, ReportMatchDrafts,
# ReportMatchColumnMappings, ReportDataUsageConsents, Templates.SchemaModelId,
# Templates.IsPublishReady, Clients.LogoBlobPath) are created by hand-authored,
# idempotent DDL in HandwrittenMigrationsBootstrapper.EnsureTablesExistAsync,
# which only runs as part of the API's own startup sequence
# (StartupDbTasksHostedService). There is no standalone CLI for it. See
# ../MIGRATIONS.md for the full history/rationale.
#
# This script therefore: (1) applies EF migrations via `dotnet ef database
# update`, then (2) actually starts the API against that database and waits
# for it to report "database ready", which is the only way to also run the
# bootstrapper + role/admin/schema-model seeders. It is safe to re-run.
#
# Usage:
#   ./setup-db.sh                                   # spins up a throwaway SQL Server in Docker
#   ./setup-db.sh --connection-string "Server=...;"  # use an existing SQL Server instead
#   ./setup-db.sh --keep-running                     # leave the API running after setup
#   ./setup-db.sh --port 5099 --timeout 300
#
# Requires: .NET 8 SDK, and either Docker (default path) or an existing
# reachable SQL Server (--connection-string).

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
BACKEND_DIR="$(cd "$SCRIPT_DIR/.." && pwd)"
API_PROJECT="$BACKEND_DIR/StudioTechBI.API"
INFRA_PROJECT="$BACKEND_DIR/StudioTechBI.Infrastructure"

CONTAINER_NAME="koru-qa-sql"
SQL_PORT="14330"
CONNECTION_STRING=""
USE_DOCKER=1
KEEP_RUNNING=0
TIMEOUT=300
API_PORT=5099

SEED_ADMIN_NAME="${SEED_ADMIN_NAME:-QA Admin}"
SEED_ADMIN_EMAIL="${SEED_ADMIN_EMAIL:-admin@studiotechbi.local}"
SEED_ADMIN_PASSWORD="${SEED_ADMIN_PASSWORD:-QaAdmin!2026}"
JWT_SECRET_VALUE="${JWT_SECRET:-}"

usage() {
  sed -n '2,25p' "${BASH_SOURCE[0]}" | sed 's/^# \{0,1\}//'
}

while [[ $# -gt 0 ]]; do
  case "$1" in
    --connection-string) CONNECTION_STRING="$2"; USE_DOCKER=0; shift 2 ;;
    --no-docker) USE_DOCKER=0; shift ;;
    --keep-running) KEEP_RUNNING=1; shift ;;
    --timeout) TIMEOUT="$2"; shift 2 ;;
    --port) API_PORT="$2"; shift 2 ;;
    -h|--help) usage; exit 0 ;;
    *) echo "Unknown argument: $1" >&2; usage; exit 1 ;;
  esac
done

log() { echo "[setup-db] $*"; }
err() { echo "[setup-db] ERROR: $*" >&2; }

API_PID=""
LOG_FILE=""
cleanup_on_interrupt() {
  if [[ -n "$API_PID" ]]; then
    err "Interrupted — stopping API (PID $API_PID)."
    kill "$API_PID" 2>/dev/null || true
  fi
  exit 130
}
trap cleanup_on_interrupt INT TERM

if [[ -z "$CONNECTION_STRING" ]]; then
  if [[ $USE_DOCKER -eq 0 ]]; then
    err "--no-docker was set but no --connection-string was given."
    exit 1
  fi
  if ! command -v docker &>/dev/null; then
    err "Docker not found. Install Docker, or pass --connection-string \"<your SQL Server>\"."
    exit 1
  fi

  SA_PASSWORD="${SA_PASSWORD:-$(openssl rand -base64 18 | tr -dc 'A-Za-z0-9')Aa1!}"

  if docker ps -a --format '{{.Names}}' | grep -qx "$CONTAINER_NAME"; then
    log "Reusing existing container '$CONTAINER_NAME'."
    docker start "$CONTAINER_NAME" >/dev/null
  else
    log "Starting a throwaway SQL Server container '$CONTAINER_NAME' on port $SQL_PORT..."
    docker run -d --name "$CONTAINER_NAME" \
      -e "ACCEPT_EULA=Y" \
      -e "MSSQL_SA_PASSWORD=$SA_PASSWORD" \
      -p "${SQL_PORT}:1433" \
      mcr.microsoft.com/mssql/server:2022-latest >/dev/null
  fi

  CONNECTION_STRING="Server=localhost,${SQL_PORT};Database=StudioTechBIDb;User Id=sa;Password=${SA_PASSWORD};TrustServerCertificate=True;"
else
  log "Using provided connection string (Docker skipped)."
fi

log "Restoring local dotnet tools (dotnet-ef)..."
( cd "$BACKEND_DIR" && dotnet tool restore )

log "Applying EF Core migrations (retrying while SQL Server finishes starting up)..."
MIGRATE_ATTEMPTS=0
until DB_CONNECTION="$CONNECTION_STRING" dotnet ef database update \
  --project "$INFRA_PROJECT" --startup-project "$API_PROJECT"; do
  MIGRATE_ATTEMPTS=$((MIGRATE_ATTEMPTS + 1))
  if [[ $MIGRATE_ATTEMPTS -ge 15 ]]; then
    err "'dotnet ef database update' failed after $MIGRATE_ATTEMPTS attempts."
    exit 1
  fi
  log "  ...attempt $MIGRATE_ATTEMPTS failed (SQL Server may still be starting), retrying in 5s"
  sleep 5
done

if [[ -z "$JWT_SECRET_VALUE" ]]; then
  JWT_SECRET_VALUE="$(openssl rand -base64 48)"
fi

log "Starting the API to run seeders and the hand-authored table bootstrapper..."
LOG_FILE="$(mktemp)"
env \
  ASPNETCORE_ENVIRONMENT="Development" \
  ASPNETCORE_URLS="http://localhost:${API_PORT}" \
  DB_CONNECTION="$CONNECTION_STRING" \
  UseDemoStorage="false" \
  SeedAdmin__Enabled="true" \
  SeedAdmin__Name="$SEED_ADMIN_NAME" \
  SeedAdmin__Email="$SEED_ADMIN_EMAIL" \
  SeedAdmin__Password="$SEED_ADMIN_PASSWORD" \
  JWT_SECRET="$JWT_SECRET_VALUE" \
  JWT_ISSUER="StudioTechBI" \
  JWT_AUDIENCE="StudioTechBIUsers" \
  dotnet run --project "$API_PROJECT" --no-launch-profile >"$LOG_FILE" 2>&1 &
API_PID=$!

log "Waiting for the app to report the database is ready (this includes the first-time build)..."
ELAPSED=0
READY=0
while [[ $ELAPSED -lt $TIMEOUT ]]; do
  if ! kill -0 "$API_PID" 2>/dev/null; then
    err "API process exited unexpectedly. Last log lines:"
    tail -n 60 "$LOG_FILE" >&2
    exit 1
  fi
  STATUS="$(curl -s -o /dev/null -w '%{http_code}' "http://localhost:${API_PORT}/api/__ready-probe" || echo "000")"
  # DatabaseReadinessMiddleware returns 503 for any /api/* path until the
  # migrate -> seed -> HandwrittenMigrationsBootstrapper -> seed sequence
  # finishes; any other status (404 here, since the path doesn't exist) means
  # it finished. Polling a real DB-connectivity endpoint like /health would
  # give a false-positive here, since /health goes green as soon as SQL
  # Server is reachable, before seeding/bootstrapping complete.
  if [[ "$STATUS" != "503" && "$STATUS" != "000" ]]; then
    READY=1
    break
  fi
  sleep 2
  ELAPSED=$((ELAPSED + 2))
done

if [[ $READY -eq 0 ]]; then
  err "Database did not become ready within ${TIMEOUT}s."
  echo "--- Recent API log ($LOG_FILE) ---" >&2
  tail -n 60 "$LOG_FILE" >&2
  kill "$API_PID" 2>/dev/null || true
  exit 1
fi

echo ""
echo "=============================================================="
echo " Database ready for testing."
echo "=============================================================="
echo " Connection string : $CONNECTION_STRING"
echo " API base URL      : http://localhost:${API_PORT}"
echo " Seeded admin email: $SEED_ADMIN_EMAIL"
echo " Seeded admin pass : $SEED_ADMIN_PASSWORD"
if [[ $USE_DOCKER -eq 1 ]]; then
  echo " Docker teardown    : docker rm -f $CONTAINER_NAME"
fi
echo " API log file       : $LOG_FILE"
echo "=============================================================="

if [[ $KEEP_RUNNING -eq 1 ]]; then
  log "Leaving API running (PID $API_PID) per --keep-running."
else
  log "Stopping API (PID $API_PID). Re-run with --keep-running to leave it up for testing."
  kill "$API_PID" 2>/dev/null || true
  wait "$API_PID" 2>/dev/null || true
fi
