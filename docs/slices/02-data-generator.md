# Slice 02 — Data generator and import (I1, I2, I3)

## What changed
- `tools/DataGenerator/` — console app, two commands: `generate` and `import`.
  - `Generation/` one small seeder per area (people, projects, milestones, tasks, risks, governance, revisions, costs, forecasts, snapshots, automation) plus `EdgeCaseChecks`.
  - `Csv/` `CsvTable` (formatting rules) and `CsvExporter` (one explicit line per column).
  - `Import/` `CsvImporter` (COPY in FK order), `DemoUserWriter` (roles + users + password hashes).
- `scripts/import-data.sh` and `.ps1` — read `.env`, build the connection string, run the importer.
- `data/csv/` — the 19 generated files (committed: they are a deliverable).
- `tests/Pmo.IntegrationTests/DataGenerator/` — 5 generator tests, 11 import tests (own Testcontainers database).
- `.env.example` / `docker-compose.yml` — `DB_HOST_PORT` (5433) and `DEMO_USER_PASSWORD`.
- `docs/database-design.md` §5 — note that EF also indexes every foreign key.

## Request flow
No HTTP yet. `generate`: `SeedGenerator.Generate(seed)` → seeders fill a `SeedData` object → `EdgeCaseChecks.AssertAll` → `CsvExporter` → files.
`import`: `MigrateAsync` → `TRUNCATE ... RESTART IDENTITY CASCADE` → `COPY` each file (users go through `DemoUserWriter`) → `setval` on every identity sequence → one transaction, commit.

## Key decisions
- **Domain classes are the in-memory model.** The generator reuses `Pmo.Domain` entities, so a renamed property breaks the build instead of silently producing a wrong CSV.
- **Targets, not luck.** Posted costs of a project are split so spend lands on a chosen share of the current budget (`SeedMath.Split`). That is why PRJ-001 is exactly 92.00% and PRJ-009 exactly 109%. Tasks, risks, CRs and revisions are hand-written plan tables for the same reason: every edge case has a named place.
- **Fixed clock, seeded random.** `SeedClock.AsOf` and `Rng(seed)` are the only sources of time and randomness (Bogus uses the same seed), so two runs produce identical bytes. A test proves it.
- **Passwords never touch a file.** `users.csv` has no hash. The importer hashes `DEMO_USER_PASSWORD` with the real Identity `PasswordHasher`, so login in slice 3 works on the first try.
- **The database is the second validator.** COPY runs through every CHECK, FK and unique index; a wrong generator fails at import.
- **Import replaces everything.** Simple and repeatable; `reporting_settings` keeps its migration row.

## Rules and where they are enforced
| Rule | Where |
|---|---|
| §10 edge-case checklist and volume targets | `EdgeCaseChecks` (throws, nothing is written) |
| Same seed ⇒ same files | `Rng`, `SeedClock`, tests `Generate_SameSeedTwice_...` |
| Posted costs and reversals valid | DB CHECKs `ck_cost_transactions_*` during COPY |
| No empty strings in CSV (COPY would turn them into NULL) | `CsvTable.Format` throws |
| Decisions never by the requester | `GovernanceSeeder` + DB CHECK on `change_requests` |

## Transaction behaviour
The whole import is one transaction. If any file fails (bad row, FK, CHECK) PostgreSQL rolls back, including the TRUNCATE, so the previous data is still there.

## Seed facts worth knowing (asOf 2026-09-30)
| Project | Current budget | Actual spend | FTC | Case |
|---|---|---|---|---|
| PRJ-001 | 1,300,000 | 1,196,000 (92%) | 90,000 | near budget |
| PRJ-002 | 2,700,000 | 1,647,000 | 1,300,000 | EAC 2,947,000 > budget |
| PRJ-003 | 800,000 | 0 (drafts only) | 500,000 | no posted spend |
| PRJ-005 | 620,000 | 248,000 | 0 | no forecast rows |
| PRJ-007 | 1,500,000 | 675,000 | 700,000 | approved revision dated 2026-11-01 ignored |
| PRJ-008 | 1,950,000 | 1,072,500 | 700,000 | pending +100,000 ignored |
| PRJ-009 | 550,000 | 599,500 (109%) | 60,000 | overspent; posted cost dated 2026-10-05 excluded |

118 tasks, 33 milestones, 27 risks (4 open high-exposure, 1 closed), 18 change requests, 8 revisions, 123 costs, 270 forecast rows, 1 pending outbox message and 1 failed one (for the worker and retry demos in slice 6).
Demo logins: `admin1@pmo.example`, `admin2@…`, `pm1…pm5@…`, `con1…con11@…`, password = your `DEMO_USER_PASSWORD`. pm4 is a plain contributor on PRJ-001 (in scope, cannot manage).

## How to demo (2 min)
1. `docker compose up -d db`, set `DEMO_USER_PASSWORD` in `.env`.
2. `dotnet run --project tools/DataGenerator -- generate --seed 20260930 --out data/csv` → note "All edge cases present". `git status` shows no change in `data/csv` (deterministic).
3. `./scripts/import-data.sh` (or `.ps1`).
4. In psql: `select code, status from projects;` and the spend query from `database-design.md` §6 for PRJ-001 → 1,196,000.00.
5. Break it on purpose: in `EdgeCaseChecks` demand `>= 50` overdue tasks, rerun `generate` → it fails and lists the missing rule.

## Likely interview questions
1. **How do you know the data is reproducible?** Seeded `Random` and Bogus seed, a fixed "today", no `Guid.NewGuid()`/`DateTime.Now`; a test builds the CSV text twice and compares.
2. **Why generate in C# and load with COPY, not seed in migrations?** Migrations are schema; data volume and edge cases change. COPY is the fast bulk path and CSVs are a deliverable others (Power BI, Access) can read.
3. **Where are the demo passwords?** Only in the `DEMO_USER_PASSWORD` environment variable; hashed by Identity at import time; CSV and git never see them.
4. **How do you guarantee the edge cases?** `EdgeCaseChecks` mirrors the §10 checklist and throws; plus the import hits DB constraints; plus SQL tests on the loaded database.
5. **Why are identity sequences reset?** Rows are loaded with explicit ids. Without `setval`, the first insert from the API would reuse id 1 and fail with a duplicate key.

## Live-change rehearsal
"Make PRJ-001 hit 95% instead of 92%": in `Generation/CostSeeder.cs` change `new("PRJ-001", 16, 0.92m, 2, false)` to `0.95m`, rerun generate + import. The utilisation test in `SeedImportTests` then fails by design (it asserts 0.92): update the expected value too. `EdgeCaseChecks` still passes (90–100%).
