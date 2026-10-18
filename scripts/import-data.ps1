# Applies migrations and loads data/csv into PostgreSQL, then creates the demo users (story I3).
# Usage: .\scripts\import-data.ps1 [-Generate]     (-Generate rebuilds the CSV files first, seed 20260930)
param([switch]$Generate)
$ErrorActionPreference = 'Stop'
Set-Location (Split-Path -Parent $PSScriptRoot)

if (Test-Path .env) {
    Get-Content .env | Where-Object { $_ -match '^\s*[^#\s][^=]*=' } | ForEach-Object {
        $name, $value = $_ -split '=', 2
        [Environment]::SetEnvironmentVariable($name.Trim(), $value.Trim(), 'Process')
    }
}
if (-not $env:DEMO_USER_PASSWORD) { throw 'Set DEMO_USER_PASSWORD in .env: the password given to every demo user' }
if (-not $env:POSTGRES_PASSWORD) { throw 'Set POSTGRES_PASSWORD in .env' }

$port = if ($env:DB_HOST_PORT) { $env:DB_HOST_PORT } else { '5433' }
$db = if ($env:POSTGRES_DB) { $env:POSTGRES_DB } else { 'pmo' }
$user = if ($env:POSTGRES_USER) { $env:POSTGRES_USER } else { 'pmo' }
$env:ConnectionStrings__Default = "Host=localhost;Port=$port;Database=$db;Username=$user;Password=$($env:POSTGRES_PASSWORD)"

if ($Generate) { dotnet run --project tools/DataGenerator -- generate --seed 20260930 --out data/csv }
dotnet run --project tools/DataGenerator -- import --csv data/csv
