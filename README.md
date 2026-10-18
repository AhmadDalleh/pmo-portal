# PMO Portfolio & Financial Control Portal

ASP.NET Core (.NET 10) API + PostgreSQL 16 + Angular 22. Requirements: `docs/assignment/Candidate_Assignment.pdf`, `docs/user-stories.md`; schema and KPI formulas: `docs/database-design.md`. Reporting date for all demos: **2026-09-30**.

## Quick start
Prerequisites: .NET 10 SDK, Docker, Node 24.

```bash
cp .env.example .env                       # then set POSTGRES_PASSWORD, DEMO_USER_PASSWORD, Jwt__Key
docker compose up -d db                    # PostgreSQL on localhost:5433
dotnet tool restore
dotnet user-secrets set "ConnectionStrings:Default" "Host=localhost;Port=5433;Database=pmo;Username=pmo;Password=<POSTGRES_PASSWORD>" --project src/backend/Pmo.Api
dotnet user-secrets set "Jwt:Key" "<32+ random characters>" --project src/backend/Pmo.Api
./scripts/import-data.sh                   # (Windows: .\scripts\import-data.ps1) migrate + load data/csv + create demo users
./scripts/reset-db.sh                      # (Windows: .\scripts\reset-db.ps1 -Yes) wipe and reload the seed; -Hard also rebuilds the schema
dotnet run --project src/backend/Pmo.Api   # API on http://localhost:5217, Swagger at /swagger
cd src/frontend/pmo-web && npm install && npm start   # http://localhost:4200 (proxies /api to the API)
```
Regenerate the data (identical output every time): `dotnet run --project tools/DataGenerator -- generate --seed 20260930 --out data/csv`.
Run tests (Docker needed for Testcontainers): `dotnet test` and `cd src/frontend/pmo-web && npx ng build`.

## Demo logins
Password for every account = the value of `DEMO_USER_PASSWORD` in your `.env`.

| Account | Role | Notes |
|---|---|---|
| `admin1@pmo.example`, `admin2@pmo.example` | Administrator | sees everything, decides budget revisions |
| `pm1@pmo.example` | Project Manager | owns PRJ-001 (near budget), PRJ-006 (on hold), PRJ-009 (overspent) |
| `pm2@pmo.example` | Project Manager | PRJ-002 (forecast overrun), PRJ-003 (draft costs only) |
| `pm3@pmo.example` | Project Manager | PRJ-004 (completed), PRJ-005 (no forecast) |
| `pm4@pmo.example` | Project Manager | PRJ-007, PRJ-010; only a contributor on PRJ-001 |
| `pm5@pmo.example` | Project Manager | PRJ-008 (pending + approved revision) |
| `con1@pmo.example` … `con11@pmo.example` | Contributor | members of 2-4 projects each |

## Architecture
`Domain` (entities and rules, no framework) → `Application` (feature services, validators, DTOs) → `Infrastructure` (EF Core, Dapper KPI SQL, Identity/JWT, outbox worker) → `Api` (thin controllers, policies, middleware). Authorization = role policy on the route + project scope check in the service (out of scope 404, not allowed 403). Details per slice: `docs/slices/`.

## Documents
`docs/architecture.md` · `docs/data-dictionary.md` · `docs/api-routes.md` (+ `docs/api/openapi.json`) · `docs/slices/*.md` (what, flow, decisions, demo, interview questions) · `docs/manual-test-checklist.md` · `docs/security-notes.md` · `docs/ai-development-log.md` · `docs/database-design.md` · `sql/reports/` · `powerbi/` · `docs/reconciliation.md` · `docs/automation-evidence.md`.

## Status
Built: A1-A6, B1-B6, C1-C7, D1-D2, E1-E5, F1-F11, G1-G6, H1-H3, I1-I3, plus the admin screens (users, audit log, automation, cost categories) and the reporting views, report queries and Power BI model notes (inputs for J). Not built: risk matrix page outside the dashboard (D3 is the dashboard visual), milestone roadmap visual, the Power BI `.pbix` (built in Power BI Desktop), Access (L), slides (K9), Docker images for api/web. Submission checklist with owners: `docs/submission-status.md`.
