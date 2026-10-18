#!/usr/bin/env bash
# Puts the database back to the seeded demo state: every table is emptied and data/csv is loaded again
# (this brings back the seeded failed outbox message 7 and the pending message 9 for the automation demo).
# Usage: ./scripts/reset-db.sh [--hard] [--yes]
#   --hard  also drops the whole schema first and lets the migrations rebuild it (use after a schema change)
#   --yes   skip the confirmation question
# Needs: .env (see .env.example) and the container pmo-db running (docker compose up -d db).
set -euo pipefail
cd "$(dirname "$0")/.."

HARD=0; YES=0
for arg in "$@"; do
  case "$arg" in
    --hard) HARD=1 ;;
    --yes) YES=1 ;;
    *) echo "Unknown option: $arg"; exit 2 ;;
  esac
done

if [ "$YES" -ne 1 ]; then
  read -r -p "This deletes ALL data in the local PMO database and reloads the seed. Type yes to continue: " answer
  [ "$answer" = "yes" ] || { echo "Cancelled."; exit 1; }
fi

if [ -f .env ]; then set -a; . ./.env; set +a; fi
DB="${POSTGRES_DB:-pmo}"; USER_NAME="${POSTGRES_USER:-pmo}"

if [ "$HARD" -eq 1 ]; then
  docker exec pmo-db psql -U "$USER_NAME" -d "$DB" -c 'SET client_min_messages = warning; DROP SCHEMA public CASCADE; CREATE SCHEMA public;'
fi

# The importer applies migrations, truncates every table and loads the CSV files in one transaction.
./scripts/import-data.sh

echo "Outbox after reset:"
docker exec pmo-db psql -U "$USER_NAME" -d "$DB" -c 'SELECT id, dedup_key, status, attempts FROM outbox_messages ORDER BY id'
