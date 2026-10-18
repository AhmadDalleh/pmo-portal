# Puts the database back to the seeded demo state: every table is emptied and data/csv is loaded again
# (this brings back the seeded failed outbox message 7 and the pending message 9 for the automation demo).
# Usage: .\scripts\reset-db.ps1 [-Hard] [-Yes]
#   -Hard  also drops the whole schema first and lets the migrations rebuild it (use after a schema change)
#   -Yes   skip the confirmation question
# Needs: .env (see .env.example) and the container pmo-db running (docker compose up -d db).
param([switch]$Hard, [switch]$Yes)
$ErrorActionPreference = 'Stop'
Set-Location (Split-Path -Parent $PSScriptRoot)

if (-not $Yes) {
    $answer = Read-Host 'This deletes ALL data in the local PMO database and reloads the seed. Type yes to continue'
    if ($answer -ne 'yes') { Write-Host 'Cancelled.'; exit 1 }
}

$envValues = @{}
if (Test-Path .env) {
    Get-Content .env | Where-Object { $_ -match '^\s*[^#\s][^=]*=' } | ForEach-Object {
        $name, $value = $_ -split '=', 2
        $envValues[$name.Trim()] = $value.Trim()
    }
}
$db = if ($envValues['POSTGRES_DB']) { $envValues['POSTGRES_DB'] } else { 'pmo' }
$user = if ($envValues['POSTGRES_USER']) { $envValues['POSTGRES_USER'] } else { 'pmo' }

if ($Hard) {
    docker exec pmo-db psql -U $user -d $db -c 'SET client_min_messages = warning; DROP SCHEMA public CASCADE; CREATE SCHEMA public;'
    if ($LASTEXITCODE -ne 0) { throw 'Could not drop the schema: is the db container running?' }
}

# The importer applies migrations, truncates every table and loads the CSV files in one transaction.
& "$PSScriptRoot\import-data.ps1"
if ($LASTEXITCODE -ne 0) { throw 'Import failed' }

Write-Host 'Outbox after reset:'
docker exec pmo-db psql -U $user -d $db -c 'SELECT id, dedup_key, status, attempts FROM outbox_messages ORDER BY id'
