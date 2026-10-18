# 15-minute interview demo script

Covers the six business journeys (J1–J6, `user-stories.md`) and acceptance tests A–H. Details and expected numbers per module: `docs/walkthrough/`.

## Before the interview (5 minutes, not part of the 15)
1. `docker compose up -d db` then `./scripts/import-data.sh` (**re-import: the numbers below assume the fresh seed**; the local DB drifts after any clicking).
2. API: `dotnet run --project src/backend/Pmo.Api` (leave it running: the outbox worker needs it). Web: `cd src/frontend/pmo-web && npm start`.
3. Open four things: browser tab 1 (UI, normal window), tab 2 (private window for a second user), Swagger (`http://localhost:5217/swagger`), psql (`docker exec -it pmo-db psql -U pmo -d pmo_db`).
4. Sign in as `pm3` in tab 1 and `con5` in the private window. Password = `DEMO_USER_PASSWORD`.
5. Know the one-liner: *Angular → API (policy + scope) → service → domain rule → EF/SQL → PostgreSQL; KPIs only in SQL.*

## Timeline
| Min | Step | Do | Say / show | Covers |
|---|---|---|---|---|
| 0:00 | **Frame** | Slide-free: show the repo tree | Clean Architecture folders, `docs/slices`, 51 unit + 190 integration tests, seed 20260930, reporting date 2026-09-30. | – |
| 1:00 | **J1 Create project** | `pm3` → Projects → New project: code `PRJ-011`, dates 2026-10-01 → 2027-03-31, budget 250000. First submit with the end date **before** the start. | Field error from the same rule as the server. Then valid: opens the project. psql: `select * from project_budgets order by id desc limit 1;` and `select action from audit_events order by id desc limit 1;` → one transaction wrote project, budget, owner membership, audit. Members tab: add `con5` (J1 owner/team). | J1, B, **A** (persistence) |
| 3:00 | **J2 Team member updates a task** | Private window `con5` → My work. Show task #4 in PRJ-001 (due 2026-09-10, 60%). Update to Done. | Tab 1 as `admin1` (or `pm1`): PRJ-001 Overview: overdue 1 → 0, progress up; dashboard filtered `?projectId=1` agrees with no refresh logic: it is SQL. | J2, C2, G5 |
| 5:00 | **J3 Manager: milestone/risk with validation** | `pm1`: PRJ-001 → Risks → New, probability 4, impact 4, **no mitigation** → blocked with the field message. Add mitigation → saved, score 16. Milestones tab: complete one (confirm dialog). | Validation mirrors the server; `score` is a generated DB column; closed risks drop out of the high-exposure KPI. | J3, D1, C5 |
| 7:00 | **J4 Finance + acceptance B** | `pm3` → PRJ-005 → Finance. Note spend AED 248,000, budget used 40%. Post draft **#66** (confirm dialog explains immutability). | Spend → **265,551**, remaining 354,449, 42.8%; charts refresh. psql: `select status, posted_at from cost_transactions where id=66;` and the audit row. Try Reverse → spend back to 248,000, original unchanged. Mention Idempotency-Key, trigger, outbox on thresholds. | J4, **B**, F5–F7 |
| 9:00 | **J5 Change request + acceptance C** | `con1` (or `con5`) in the private window: PRJ-001 → Change requests → New → Submit. `pm1`: Approvals → Approve with a reason. | CR timeline shows 3 steps with names. psql: `select to_status from change_request_history where change_request_id=<id> order by id;` (3 rows) and `select status from outbox_messages order by id desc limit 1;`. Within ~10 s the bell of the requester shows 1 (worker). Budget unchanged (E5). | J5, **C**, E1–E4, H1 |
| 11:00 | **Acceptance D: denial** | Swagger as `con1` (Authorize with its token): `POST /api/approvals/change-requests/14/decision` → **403**; `POST /api/finance/costs/66/post` → **403**; `GET /api/projects/2` → **404**. As `pm1` approve your own request → 403 `self_approval_forbidden`. | Two layers: policy then scope; 404 hides existence; DB CHECK as a second guard (`23514` in psql). | **D**, A3 |
| 12:00 | **J6 Dashboard + acceptance E/F** | `admin1` → Dashboard. Filter **Project manager = Chanelle Stamm**: budget 2,850,000, spend 2,175,500, EAC 2,525,500, overrun 1 project / 109,500. Click **Actual spend**: total equals the card. Click the "Overdue tasks by owner" bar, then a risk-matrix cell. Change a filter, show the URL; copy it to a new tab. | Same `fp` CTE for KPI, charts and drill-down; category filter scope; formula tooltips (G7). Analytics agreement (F): run the independent SQL from `walkthrough/07-dashboard.md` and say Power BI/J5 were not built. | J6, **E**, **F**, G1–G4 |
| 13:30 | **Acceptance G + H** | Swagger as `admin1`: `GET /api/admin/automation/outbox?status=failed` → id 7; `POST …/7/retry`. Then the two-tab concurrency demo: open the same project edit in two tabs, save both → 409. | Outbox: SKIP LOCKED, dedup, backoff, retry. Quality: ProblemDetails + correlation id, structured logs, `xmin` versions, idempotency. | **G** (Access/VBA not built), **H** |
| 14:30 | **Close** | Show `docs/walkthrough/00-differences.md` | Be upfront: what differs from the docs, what is not built (Power BI, slides, Access, admin UI, Docker images). Offer a live change. | – |

## If something breaks
- **Numbers differ:** you did not re-import. Run `./scripts/import-data.sh` (30 s) and refresh.
- **No notification:** the API must be running 10 s after the approval; check `select * from outbox_messages order by id desc limit 3;` and `automation_runs`.
- **Login fails:** `DEMO_USER_PASSWORD` in `.env` at import time is the password; lockout: `update users set lockout_end=null, access_failed_count=0;`.
- **Charts blank:** resize the window once (ECharts layout), or open the page in a new tab.

## Live-change warm-ups (pick any, each is one line)
| Request | Edit |
|---|---|
| Amber at 85% | `ProjectHealth.Evaluate` (0.90m → 0.85m) + its unit test row |
| Overdue grace of 3 days | `TaskRules.Overdue` and the `due_date < …` in the SQL files |
| Lockout 10 minutes | `Infrastructure/DependencyInjection.cs` `DefaultLockoutTimeSpan` |
| Fail after 3 attempts | `OutboxBatchRunner.MaxAttempts` |
| Alert at 80% | `CostService.QueueThresholdMessagesAsync` |
| Due-soon window 7 days | `TaskRules.DueSoonDays` |

## Acceptance tests at a glance
| Test | Minute | Proof |
|---|---|---|
| A End-to-end persistence | 1:00, 3:00 | project, budget, membership, task progress survive reload and are in SQL |
| B Finance mutation | 7:00 | post changes spend/remaining/utilisation; drill-down agrees |
| C Approval transaction | 9:00 | one history row, audit, outbox, atomic |
| D Authorization denial | 11:00 | 403 / 404 / self-approval |
| E Dashboard integrity | 12:00 | KPI = drill-down = raw SQL |
| F Analytics agreement | 12:00 | SQL vs web (Power BI not built) |
| G Automation | 13:30 | worker, retry (VBA not built) |
| H Engineering quality | 13:30 | 409 concurrency, ProblemDetails, idempotency |
