#!/usr/bin/env bash
# Applies migrations and loads data/csv into PostgreSQL, then creates the demo users (story I3).
# Usage: ./scripts/import-data.sh [--generate]     (--generate rebuilds the CSV files first, seed 20260930)
# Needs: .env (see .env.example) or the same variables in the environment, and a running database (docker compose up -d db).
set -euo pipefail
cd "$(dirname "$0")/.."

if [ -f .env ]; then set -a; . ./.env; set +a; fi
: "${DEMO_USER_PASSWORD:?Set DEMO_USER_PASSWORD in .env: the password given to every demo user}"
export DEMO_USER_PASSWORD
export ConnectionStrings__Default="Host=localhost;Port=${DB_HOST_PORT:-5433};Database=${POSTGRES_DB:-pmo};Username=${POSTGRES_USER:-pmo};Password=${POSTGRES_PASSWORD:?Set POSTGRES_PASSWORD in .env}"

if [ "${1:-}" = "--generate" ]; then
  dotnet run --project tools/DataGenerator -- generate --seed 20260930 --out data/csv
fi
dotnet run --project tools/DataGenerator -- import --csv data/csv
