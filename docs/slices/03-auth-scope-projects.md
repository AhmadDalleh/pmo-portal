# Slice 03 — Auth, project scope, projects (A1–A3, B1–B6)

## What changed
- **Application** (`Pmo.Application`): `IPmoDbContext`, `ICurrentUser`, `IProjectScope` + `ProjectScope`, `ProjectAccess` (managed-project rule), `IUserDirectory`, `ReportingOptions`;
  `Features/Projects` (`ProjectService`, `ProjectMemberService`, `ProjectHealth`, commands, DTOs, validators, `IProjectReadModel`), `Features/Lookups`, `Features/Auth` (DTOs).
- **Infrastructure**: `Identity/` (`AuthService`, `JwtTokenService`, `RefreshTokenService`, `UserDirectory`), `Analytics/ProjectPortfolio.sql` + `ProjectReadModel` (Dapper), `AddAuth` wiring.
- **Api**: `AuthController`, `ProjectsController`, `ProjectMembersController`, `LookupsController`, `Authorization/` (policies, `CurrentUser`), CORS allow-list, login rate limit, Swagger bearer button, exception handler now maps validation (400), failed sign-in (401) and unique-index races (409).
- **Tests**: 8 unit + 71 integration (all use the seeded database). Test factory now imports the seed and logs in through the real endpoint.
- Docs: `docs/api-routes.md` (new), refresh-token note in `database-design.md`, `CLAUDE.md` commands.

## Request flow (example: `GET /api/projects/2` as con1)
Angular `ProjectsApi` → HTTP GET → JWT bearer middleware validates the token → `ProjectsController.Get` → `ProjectService.GetAsync` → `IProjectScope.EnsureCanViewAsync` (reads `project_members`) → not in scope → `NotFoundException` → `GlobalExceptionHandler` → 404 ProblemDetails with correlation id.
Sign-in: `POST /api/auth/login` → `AuthService` (Identity `UserManager`: find, lockout check, password hash check) → audit row → `JwtTokenService` (15 min) + `RefreshTokenService` (row in `user_tokens`) → body + HttpOnly cookie.

## Key decisions
- **Application depends on `IPmoDbContext`, not on Npgsql.** Services use LINQ directly (no generic repository). The DbContext in Infrastructure implements the interface. KPI SQL stays in Infrastructure behind `IProjectReadModel`.
- **Two authorization layers.** `[Authorize(Policy)]` checks the role; the service then asks `IProjectScope`. Out of scope = 404 (hides existence), in scope but not allowed = 403.
- **No new table for refresh tokens.** Identity's own `user_tokens` stores a SHA-256 hash of the secret plus expiry; every refresh rotates (old row deleted). Trade-off: no device list/UI.
- **KPI SQL is one embedded file** (`ProjectPortfolio.sql`), one CTE per one-to-many table, used by the list, details and (later) the finance summary. The health badge is a small C# function (`ProjectHealth`) over those numbers so it can be unit tested.
- **Generic sign-in failure.** Unknown user, wrong password, inactive and locked all return the same 401 text; the real reason goes to `audit_events` only.

## Rules and where they are enforced
| Rule | Where |
|---|---|
| 15-minute JWT, claims sub/email/name/role | `JwtTokenService`, validated in `AddAuth` |
| Refresh cookie HttpOnly, Secure, SameSite=Strict, Path=/api/auth | `AuthController.CookieOptions` |
| Lockout after 5 failures for 5 minutes; inactive cannot sign in | Identity options in `AddAuth`; `AuthService` |
| Role gate on create/edit/members | `[Authorize(Policy = ManageProjects)]` |
| Scope (404) and manage (403) | `ProjectScope` called first in every service method |
| Owner must be an active Project Manager | `ProjectService.EnsureProjectManagerAsync` (400) |
| Code unique | service pre-check (409) + `ix_projects_code` UNIQUE + handler fallback for races |
| Dates, budget, status, completed needs actual end | FluentValidation validators (400 + field errors) and DB CHECKs |
| Stale edit | `Version` mapped to `xmin`, set as original value (409 `concurrency_conflict`) |
| Cannot remove owner / member with open tasks | `ProjectMemberService.RemoveAsync` (409 with task list) |
| Sort field allow-list | `ProjectSearchValidator.SortKeys` + fixed map in `ProjectReadModel` |

## Transaction behaviour
- **Create project**: explicit transaction. Save 1 inserts the project (needs its generated id); save 2 inserts budget, owner membership and the audit event; commit. If step 2 fails, leaving the `using` block without `Commit` rolls back the project too. A test forces this with a trigger on `project_budgets`.
- **Update project / change owner**: one `SaveChanges` (project, membership rows, audit) = one database transaction.

## How to demo (2 min)
1. `dotnet user-secrets set "Jwt:Key" "<32+ chars>" --project src/backend/Pmo.Api`, start the API, open `/swagger`.
2. `POST /api/auth/login` as `pm1@pmo.example` → copy `accessToken` → Authorize. `GET /api/projects` shows 3 manageable projects in the `/auth/me` list.
3. Log in as `con1@pmo.example`: `GET /api/projects/2` → 404; `POST /api/projects` → 403.
4. As pm1: `POST /api/projects` → 201 + Location; in psql `select * from project_budgets where project_id = <id>` and `select action from audit_events order by id desc limit 3`.
5. `PUT` the same project twice with the same `version` → second call 409 `concurrency_conflict`.

## Likely interview questions
1. **Why 404 for out-of-scope but 403 for forbidden?** 404 does not reveal that an id exists; 403 is honest once the user may already see the project.
2. **Where is the role checked and where is the project checked?** Policy attribute (role) on the controller; `IProjectScope` inside the service (project). Frontend hiding is cosmetic.
3. **How do refresh tokens work and what if one leaks?** Random secret, only its hash stored, cookie is HttpOnly/Strict/path-limited, rotated on each use, so a copy works once; logout deletes it.
4. **How do you avoid double-counting in the portfolio query?** Each one-to-many table (tasks, risks, CRs, revisions, costs, forecasts) is aggregated per project in its own CTE before joining (database-design §7).
5. **How does optimistic concurrency work here?** `Version` is PostgreSQL's `xmin`; the update sets it as the original value, so EF adds `WHERE xmin = @v`; zero rows updated raises `DbUpdateConcurrencyException`, mapped to 409.

## Live-change rehearsal
"Make Amber start at 85% utilisation": change `0.90m` to `0.85m` in `ProjectHealth.Evaluate` and the same value in the `[InlineData(false, false, 0, 0.90, 0, "amber")]` unit test row. (The `near_budget` alert stays at 90% in `ProjectPortfolio.sql`, which is the KPI definition in database-design §6.)
