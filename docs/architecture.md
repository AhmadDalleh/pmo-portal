# Architecture note

## Big picture
```
 Browser (Angular 22)                      ASP.NET Core API (.NET 10)                          PostgreSQL 16
 ┌───────────────────────┐   HTTPS/JSON    ┌───────────────────────────────────────────┐      ┌────────────────────┐
 │ pages + shared widgets│  Bearer JWT     │ Api: controllers (thin), policies,        │      │ tables (EF Core    │
 │ signals, interceptors │ ──────────────▶ │      middleware (correlation id, errors)  │      │ migrations)        │
 │ no KPI maths          │ ◀────────────── │ Application: feature services, validators │ ───▶ │ constraints,       │
 └───────────────────────┘  ProblemDetails │      DTOs, transactions                   │      │ triggers, indexes  │
        refresh cookie (HttpOnly)           │ Domain: entities + rules (no framework)   │      │ reporting views    │
                                            │ Infrastructure: EF Core, Dapper KPI SQL,  │      └─────────▲──────────┘
                                            │      Identity/JWT, outbox worker          │                │
                                            └───────────────▲───────────────────────────┘      Power BI / Access / psql
                                                            │ BackgroundService (every 10 s)     read the views
                                                            └── outbox → notifications, automation_runs
```
Dependencies point inwards: `Api → Application → Domain`; `Infrastructure` implements the interfaces `Application` declares. The domain has no EF or ASP.NET references.

## Layers and where to find things
| Layer | Folder | Responsibility |
|---|---|---|
| Domain | `src/backend/Pmo.Domain` | Entities and state transitions as methods (`CostTransaction.Post`, `ChangeRequest.Decide`) that throw `DomainRuleException(code, message)` |
| Application | `src/backend/Pmo.Application/Features/<Feature>` | Services own business rules and transactions; command and query records; FluentValidation validators |
| Infrastructure | `src/backend/Pmo.Infrastructure` | `PmoDbContext` and configurations, migrations, Dapper read models with embedded `.sql` (`Analytics/`), Identity and JWT, outbox worker, daily snapshot job |
| Api | `src/backend/Pmo.Api` | Controllers, authorization policies, exception handling to ProblemDetails, Serilog, rate limiting, CORS, Swagger |
| Frontend | `src/frontend/pmo-web` | See `src/frontend/pmo-web/README.md` |

## One request end to end (post a cost)
1. Angular `FinanceApi.post` sends `POST /api/finance/costs/{costId}/post` (with the cost `version`), the bearer token and an `X-Correlation-Id`.
2. Pipeline: correlation middleware, JWT validation, fallback policy (sign-in required), route policy `CanPostCosts`.
3. `CostsController` calls `CostService.PostAsync`, which loads the cost, checks project scope (`IProjectScope.EnsureCanManageAsync`; out of scope gives 404, not allowed 403), and calls `cost.Post(actorId, now)`.
4. One transaction: update cost status, insert `audit_events`, insert `outbox_messages` if a budget threshold was newly crossed, one `SaveChanges`, commit. A failure anywhere rolls everything back.
5. The response is the updated DTO. The UI reloads the summary; the dashboard recomputes the KPIs from SQL on the next read.

## Key decisions and trade-offs
| Decision | Why | Trade-off |
|---|---|---|
| Source of truth = transactional tables; KPIs are always computed in SQL on read | No stale or client-side numbers; one definition set (`database-design.md` §6) shared by API, views and Power BI | Each dashboard load runs a few aggregate queries; fine at this size |
| Application services, no MediatR, no generic repository | Easy to explain and demo; one class per use case | A little repetition between services |
| EF Core for writes, Dapper plus embedded SQL for analytics | Writes need change tracking and `xmin` concurrency; KPIs need explicit, reviewable SQL with one CTE per one-to-many table | Two data access styles |
| Authorization in two layers (role policy, then project scope in the service) | A route can be reached directly; the scope check protects the resource, not the menu | Every service method must remember the check; integration tests cover the sensitive routes |
| Constraints and triggers in the database as a second line (immutable posted costs, append-only audit and history, no self-approval CHECK) | Rules hold even if application code is bypassed | Logic lives in two places; each trigger is covered by a test |
| Optimistic concurrency with `xmin` and an `Idempotency-Key` for cost creation | Two tabs cannot overwrite each other (409); a double click creates one cost | Clients must send `version` back |
| Outbox plus background worker for automation | The business transaction never depends on notification delivery; retries and de-duplication are safe | Delivery is eventual (seconds) |
| Reporting views read the date from `reporting_settings` | Same fixed snapshot (2026-09-30) as the dashboard default; production would use today | The views do not take a parameter |
| One role per user | Simple policy model matching the assignment's three roles | No combined roles |

## Reliability and security pointers
- Correlation id on every request, response header and error body; Serilog structured logs (console and rolling file `logs/pmo-.log`), no secrets logged.
- Errors: RFC 7807 with stable `code`; unknown errors give a generic 500 with the id.
- Security measures, token and cookie handling, CORS, injection and over-posting protections: `docs/security-notes.md`.
- Tests: 51 unit and 195 integration tests (Testcontainers PostgreSQL); the coverage map is in `docs/manual-test-checklist.md`.

## Not built (known limits)
Docker images for api and web (compose runs only the database), hosted deployment, milestone roadmap visual, frontend unit tests, row-level security for Power BI.
