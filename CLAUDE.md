# CLAUDE.md — PMO Portfolio & Financial Control Portal

This is Ahmad's take-home assessment for e& (etisalat). In the interview he must **demo, walk through the code, explain every decision, and make a live change in any file**. Code he cannot explain is a failure, even if it works. Optimize for *clear, small, explainable* over *clever or complete*.

## Source documents (read before working on a feature)
- `docs/assignment/Candidate_Assignment.pdf` — the original brief (authoritative requirements)
- `docs/user-stories.md` — stories, acceptance criteria, permission matrix, API routes, build order
- `docs/database-design.md` — tables, constraints, indexes, **KPI formulas (§6)**, transactions, seed edge cases
- `.claude/skills/pmo-feature-slice/SKILL.md` — the step-by-step workflow for building any feature

If code and docs disagree, **stop and ask** which is right; then update the doc in the same commit.

## How to work with Ahmad (non-negotiable)
1. **One vertical slice at a time** (one story or a few related ones). Never scaffold many features at once.
2. **Plan first.** Before writing code, state: stories covered, files to create/change, DB changes, endpoints, tests. Wait for "go" on anything that adds a package, a table, or a new pattern.
3. **Explain as you go.** After each slice, give the "Explain-back" summary defined in the skill (what, why, request flow, how to demo, likely interview questions).
4. **No magic.** Prefer explicit code over reflection-heavy helpers, generic repositories, auto-mappers, or source generators. Hand-map DTOs. No libraries not listed below without asking.
5. **Small files.** Target < 200 lines per file; one class per file; no "Utils" dumping grounds.
6. **Never fake it.** No hardcoded business arrays in the frontend, no in-memory stores, no client-side KPI math, no stubbed endpoints presented as done. If something is unfinished, say so.
7. **Log AI assistance.** When a slice involved a meaningful prompt/iteration, or you corrected an earlier AI mistake, draft an entry in `docs/ai-development-log.md` (template inside). Ahmad edits "what I accepted/rejected" himself.
8. If you notice a bug, insecure pattern, or a formula that doesn't match `database-design.md` §6 — **say so explicitly**, even if not asked.

## Tech stack (fixed)
| Area | Choice |
|---|---|
| Backend | ASP.NET Core Web API on **.NET 10**, controllers (not minimal APIs) |
| Architecture | Clean Architecture: `Domain` → `Application` → `Infrastructure` → `Api` |
| App layer | Feature folders with application services + command/query records (no MediatR) |
| Validation | FluentValidation; errors as RFC 7807 `ProblemDetails` |
| Data | PostgreSQL 16, EF Core + Npgsql, EFCore.NamingConventions (`snake_case`); Dapper for analytics SQL |
| Auth | ASP.NET Core Identity (`IdentityUser<int>`) + JWT bearer (15 min) + refresh token in HttpOnly cookie; resource-based authorization handlers |
| Logging | Serilog (console + rolling file), correlation-id middleware |
| Background | `BackgroundService` outbox processor |
| Frontend | Angular (current stable), standalone components, signals, Angular Material, ngx-echarts |
| Tests | xUnit, FluentAssertions-style asserts via `Shouldly`, `WebApplicationFactory`, Testcontainers PostgreSQL |
| Data gen | C# console + Bogus, fixed seed `20260930` |
| Infra | Docker Compose (postgres, api, web), `.env` (not committed), `.env.example` (committed) |

## Repository layout
```
src/backend/Pmo.Domain/            entities, enums, domain rules (no EF, no ASP.NET refs)
src/backend/Pmo.Application/       Features/<Feature>/{Commands,Queries,Dtos,Validators,<Feature>Service.cs}, abstractions
src/backend/Pmo.Infrastructure/    Persistence/{PmoDbContext,Configurations,Migrations}, Analytics/ (Dapper SQL), Identity/, Outbox/
src/backend/Pmo.Api/               Controllers/, Middleware/, Authorization/, Program.cs
src/frontend/pmo-web/              core/ (auth, http interceptors, api clients), shared/ (table, form, chart, state components), features/<feature>/
tests/Pmo.UnitTests/               domain rules, financial calculations, transitions
tests/Pmo.IntegrationTests/        API + RBAC + DB via Testcontainers
tools/DataGenerator/               seed generator + CSV export + import
sql/reports/                       commented report queries
scripts/                           import-data.sh/.ps1, reset-db
data/csv/                          generated CSVs (committed, they are a deliverable)
docs/                              specs, logs, notes
powerbi/  access/                  reporting deliverables
```

## Commands
```bash
cp .env.example .env                                      # once; then edit the password
docker compose up -d db                                   # database only
dotnet tool restore                                       # installs the pinned dotnet-ef
dotnet user-secrets set "ConnectionStrings:Default" "Host=localhost;Port=5432;Database=pmo;Username=pmo;Password=<from .env>" --project src/backend/Pmo.Api
dotnet build
dotnet test                                               # needs Docker for Testcontainers
dotnet ef migrations add <Name> -p src/backend/Pmo.Infrastructure -s src/backend/Pmo.Api -o Persistence/Migrations
dotnet ef database update      -p src/backend/Pmo.Infrastructure -s src/backend/Pmo.Api
dotnet run --project tools/DataGenerator -- generate --seed 20260930 --out data/csv
./scripts/import-data.sh                                  # migrate + COPY CSVs + demo users
dotnet run --project src/backend/Pmo.Api                  # Swagger at /swagger
cd src/frontend/pmo-web && npm start                      # http://localhost:4200
```
Update this section when commands change.

## Backend conventions
- **Controllers are thin:** bind request → call service → map result to HTTP. No EF or business logic in controllers.
- **Services own business rules** and transactions. Transitions (CR, cost posting, revisions) are methods on the domain entity that throw a `DomainRuleException(code, message)`; services catch nothing they can't handle.
- **Exceptions → HTTP** in one place (`ExceptionHandlingMiddleware` / `IExceptionHandler`):
  `ValidationException`→400, `NotFoundException`→404, `ForbiddenException`→403, `DomainRuleException`→409 (or 400 for input rules), `DbUpdateConcurrencyException`→409 `concurrency_conflict`, unknown→500 (logged with correlation id; no stack trace to client).
- **Status codes:** POST create → 201 + Location; PUT/PATCH → 200 with updated DTO (includes new `version`); state-transition POST → 200; DELETE → 204.
- **Scope on every query.** Use `ICurrentUser` + `IProjectScope` (returns accessible project ids / `CanManage(projectId)`). Out-of-scope → 404, in-scope but not allowed → 403.
- **Authorization is two layers:** `[Authorize(Policy = ...)]` for role, plus an explicit resource check in the service (`EnsureCanManage(projectId)`). Never rely on the frontend.
- **Transactions:** one `SaveChangesAsync` inside `BeginTransactionAsync` for multi-row business actions (entity + history + audit + outbox). Explain the rollback path in the explain-back.
- **Concurrency:** entities expose `uint Version` mapped to `xmin` (`.IsRowVersion()`); update DTOs carry `version`; set `OriginalValue` before save.
- **Money:** `decimal` in C#, `numeric(18,2)` in DB. Round only for display.
- **Dates:** `DateOnly` for business dates, `DateTimeOffset` (UTC) for timestamps. Reporting `asOf` comes from the request, default `Reporting:DefaultAsOf` (2026-09-30). Never use `DateTime.Now` in business logic — inject `TimeProvider`.
- **KPI SQL** lives in `Infrastructure/Analytics/*.sql` (embedded resources) or constants, parameterized via Dapper, matching `database-design.md` §6 exactly. Aggregate each one-to-many table in its own CTE (no join fan-out).
- **Never** string-concatenate SQL with user input. Sort fields are mapped from an allow-list.
- **Logging:** structured (`_logger.LogInformation("Cost {CostId} posted by {UserId}", ...)`), no PII beyond user id, never tokens/passwords.

## Frontend conventions
- Feature folders; standalone components; one API service per backend resource in `core/api/`.
- State: signals in a per-feature store service; after any mutation, **refetch from the API** — never patch KPI values locally.
- Shared components: `data-table` (sort/page/empty/loading/error), `form-field` wrappers, `confirm-dialog`, `status-badge`, `kpi-card`, `chart-card`.
- Forms: reactive forms, client validation mirrors server rules, but server `ProblemDetails.errors` are mapped onto fields.
- Every async view has loading / empty / error (with correlation id + retry) states.
- Consequential actions (post cost, decide CR, reverse, delete draft) use `confirm-dialog`.
- Accessibility: keyboard-reachable controls, labels on inputs, status shown with text/icon (not colour only), AA contrast.
- HTTP interceptors: attach token, refresh on 401 once, add `X-Correlation-Id`, map errors.
- Dashboard filters live in URL query params.

## Database rules
- Schema changes **only via EF migrations**; raw SQL (triggers, views, partial indexes) via `migrationBuilder.Sql(...)` in a migration, with a comment.
- Every new table: PK, FKs with `OnDelete(Restrict)` unless documented, CHECK constraints from the design doc, indexes for its filters.
- Update `docs/database-design.md` in the same commit as any schema change.

## Security checklist (apply to every endpoint)
- [ ] `[Authorize]` + correct policy  - [ ] resource scope check in service  - [ ] input validator
- [ ] no over-posting (explicit request DTOs)  - [ ] parameterized SQL  - [ ] audit event for sensitive mutations
- [ ] integration test proving a Contributor/out-of-scope user is denied (for sensitive routes)
Secrets: `dotnet user-secrets` locally, environment variables in Docker. Never commit `.env`, keys, or passwords. CORS: allow-list `http://localhost:4200` (+ hosted URL). Refresh cookie: `HttpOnly; Secure; SameSite=Strict; Path=/api/auth`.

## Testing
- Each slice ships with tests: unit tests for rules/calculations, integration tests for API + RBAC + persistence.
- Test names describe the rule: `PostCost_AsContributor_Returns403`.
- Financial tests assert exact decimals against seeded fixtures; include the edge cases (draft, post-cutoff, reversal, pending revision, no forecast, zero budget).
- Keep `docs/manual-test-checklist.md` in sync with acceptance tests A–H.

## Git
- Small commits, conventional style: `feat(finance): post cost with audit + outbox`, `test(rbac): ...`, `fix(dashboard): ...`, `docs: ...`.
- One slice ≈ one to a few commits. Never commit generated secrets, `bin/`, `obj/`, `node_modules/`.

## Definition of done (per slice)
1. Acceptance criteria in `docs/user-stories.md` met against the real DB
2. Server-side authorization + scope + validation + ProblemDetails errors
3. Atomic transaction where the story requires it
4. Tests written and passing (`dotnet test`)
5. UI has loading/empty/error/success states (if the slice has UI)
6. Docs updated (design doc, API routes, checklist)
7. Explain-back summary delivered to Ahmad
8. AI log entry drafted if applicable

## Do not
- Add Power BI embeds, static chart data, or mock KPIs
- Implement optional features (Gantt, dependencies, exports) before the core build order is done
- Introduce MediatR, AutoMapper, generic repository, CQRS frameworks, or microservices
- Silence an error, catch-and-ignore, or weaken a CHECK constraint to make a test pass
- Rewrite large working areas without being asked
