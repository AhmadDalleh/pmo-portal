# User Stories — Project Portfolio & Financial Control Portal

Roles: **CON** = Contributor · **PM** = Project Manager · **ADM** = Administrator
"Scope" = projects the user may see: ADM → all; PM/CON → projects where they are in `project_members`.
Reporting date for demos: **2026-09-30**. All rules below are enforced **on the server**; the UI only mirrors them.
Field definitions and KPI formulas live in `database-design.md` (§3 and §6) — this file never redefines them.

## Contents
- Permission matrix
- Epic A — Identity, access & activity (Module 07)
- Epic B — Project portfolio (Module 01)
- Epic C — Tasks & milestones (Module 02)
- Epic D — Risks & issues (Module 03)
- Epic E — Change requests & approvals (Module 04)
- Epic F — Financial management (Module 05)
- Epic G — Executive dashboard (Module 06)
- Epic H — Business automation (§8.1)
- Epic I — Data generation & import (§6)
- Epic J — Reporting track: SQL & Power BI (§7)
- Epic K — Quality, reliability & delivery (§3.5, §10)
- Epic L — Optional: MS Access / VBA (§8.2)
- Demonstrable business journeys → story map
- Live acceptance tests → story map
- Suggested build order

---

## Permission matrix

| Action | CON | PM (member as manager) | ADM |
|---|---|---|---|
| View projects | in scope | in scope | all |
| Create project | ✗ | ✓ (becomes owner) | ✓ |
| Edit project | ✗ | own/managed | ✓ |
| Manage members | ✗ | managed projects | ✓ |
| Create/edit tasks & milestones | ✗ | managed projects | ✓ |
| Update task progress/status | **own assigned tasks only** | managed projects | ✓ |
| Create/edit risks | ✗ | managed projects | ✓ |
| Submit change request | in scope | in scope | ✓ |
| Decide change request | ✗ | managed projects, **never own** | ✓, never own |
| Create draft cost | ✗ | managed projects | ✓ |
| Post cost / create reversal | ✗ (**403**) | managed projects | ✓ |
| Request budget revision | ✗ | managed projects | ✓ |
| Decide budget revision | ✗ | ✗ | ✓, never own |
| Edit forecasts | ✗ | managed projects | ✓ |
| Manage cost categories | ✗ | ✗ | ✓ |
| Dashboard | in scope | in scope | all |
| Manage users/roles, audit log | ✗ | ✗ | ✓ |

Out-of-scope resource access returns **404** (does not reveal existence); in-scope but forbidden action returns **403** with a `ProblemDetails` code.

---

## Epic A — Identity, access & activity

**A1 · Sign in** — As any user, I sign in with email and password so that the system knows who I am.
- Given a seeded demo account, when I submit valid credentials, then I receive a short-lived JWT access token (15 min) and a refresh token in an `HttpOnly; Secure; SameSite=Strict` cookie.
- Invalid credentials → 401 with a generic message (no "user exists" hints); 5 failed attempts → lockout 5 min (Identity lockout).
- Inactive users cannot sign in. Successful and failed logins write `audit_events`.
- API: `POST /api/auth/login`, `POST /api/auth/refresh`, `POST /api/auth/logout`, `GET /api/auth/me`

**A2 · Role-based navigation** — As a user, I see only the menus my role allows so the app is uncluttered.
- Menus come from `/api/auth/me` (roles + managed project ids). Hiding a menu is cosmetic; A3 is the protection.

**A3 · Server-side scoped access** — As the organization, every sensitive route checks role **and** resource scope so data cannot leak by guessing IDs.
- Every query for project-owned data filters by the caller's scope.
- `GET /api/projects/{id}` for a project outside scope → 404. Mutations on in-scope resources without permission → 403.
- A CON calling `POST /api/approvals/change-requests/{id}/decision` or `POST /api/finance/costs/{id}/post` directly (Postman/curl) → **403**, even with a valid token. Covered by automated tests.

**A4 · User & role administration** — As an ADM, I list users and change a user's role or active flag.
- ADM cannot remove their own Administrator role (prevents lock-out).
- Each change writes `audit_events` (`user.role_changed`, before/after).
- API: `GET /api/admin/users`, `PUT /api/admin/users/{id}/role`, `PUT /api/admin/users/{id}/status`

**A5 · Activity / audit review** — As an ADM, I filter the audit log by entity, action, actor, project and date.
- Paged, newest first; shows correlation id so a log line can be traced.
- API: `GET /api/admin/audit-events?entityType=&action=&actorId=&projectId=&from=&to=&page=`

**A6 · Notifications inbox** — As any user, I see my in-app notifications and mark them read.
- API: `GET /api/notifications`, `POST /api/notifications/{id}/read`

---

## Epic B — Project portfolio

**B1 · Create project** — As a PM, I create a project with code, name, department, owner, start/planned end dates and initial approved budget (AED) so delivery and cost tracking can start.
- Validation: required fields; `planned_end_date >= start_date`; budget ≥ 0; code unique (409 if duplicate).
- Project, `project_budgets` row, owner membership (`manager`) and an audit event are committed in **one transaction**.
- Owner must have the ProjectManager role (ADM may assign any PM; a PM creating a project becomes owner).
- API: `POST /api/projects` → 201 + `Location`

**B2 · Edit project** — As the owning PM or ADM, I edit name, description, dates, owner and status.
- Status `completed` requires `actual_end_date`. Request carries `version`; stale → 409 with current data.
- API: `PUT /api/projects/{id}`

**B3 · Search, filter, sort, page** — As any user, I search projects by text and filter by department, owner, status and date range, with sorting and pagination.
- Server-side; defaults: page 1, size 20, sort by code. Unknown sort field → 400.
- Each row shows code, name, department, owner, status, progress %, utilization %, health badge.
- API: `GET /api/projects?search=&departmentId=&ownerId=&status=&from=&to=&sort=&dir=&page=&pageSize=`

**B4 · Project details** — As any user in scope, I open a project and see its header, delivery status (progress, overdue tasks, next milestone), open risks, pending CRs and financial summary.
- Health rule (documented): **Red** if overspent or projected overrun or ≥ 3 overdue tasks; **Amber** if utilization ≥ 90% or any overdue task or open high-exposure risk; otherwise **Green**.
- All numbers come from the API using `database-design.md` §6.
- API: `GET /api/projects/{id}`, `GET /api/projects/{id}/summary?asOf=`

**B5 · Manage members** — As the owning PM or ADM, I add/remove members and set `manager`/`contributor`.
- Cannot remove the owner. Removing a member who still has assigned open tasks → 409 listing those tasks.
- API: `GET/POST /api/projects/{id}/members`, `DELETE /api/projects/{id}/members/{userId}`

**B6 · Lookups** — As a user filling forms, I get departments, users (PMs, members), categories.
- API: `GET /api/lookups/departments`, `/users?role=`, `/cost-categories`

---

## Epic C — Tasks & milestones

**C1 · Create/edit task** — As a PM, I create and edit tasks with title, assignee, status, priority, start/due dates, weight (1–10), progress (0–100) and optional milestone.
- Assignee must be a project member; milestone must belong to the same project (DB composite FK too).
- `status = done` ⇒ progress forced to 100 and `completed_at` set. Moving out of `done` clears `completed_at`.
- API: `GET /api/projects/{id}/tasks?status=&assigneeId=&priority=&overdue=&sort=&page=`, `POST /api/projects/{id}/tasks`, `PUT /api/tasks/{taskId}`

**C2 · Update my task** — As a CON, I update progress and status (not dates, weight or assignee) on tasks assigned to me so the project progress reflects reality.
- Task not assigned to me → 403. Version check → 409 on conflict.
- After saving, project progress and dashboard KPIs change on the next fetch (no client-side patching).
- API: `PATCH /api/tasks/{taskId}/progress` `{ progressPct, status, version }`

**C3 · My work** — As any user, I see my assigned tasks across projects, with overdue and due-soon indicators.
- API: `GET /api/me/tasks?status=&page=`

**C4 · Overdue & upcoming indicators** — As any user, I can tell at a glance what is overdue, due today, or due within 14 days.
- Overdue = `due_date < asOf` and not done/cancelled; a task due exactly on asOf is "Due today" (not overdue). Icon + text, not colour alone.

**C5 · Milestones** — As a PM, I create, edit and complete milestones.
- Completing sets `completed_at`. Overdue milestone = pending and `due_date < asOf`.
- API: `GET/POST /api/projects/{id}/milestones`, `PUT /api/milestones/{id}`, `POST /api/milestones/{id}/complete`

**C6 · Project progress calculation** — As management, I trust project progress because it is the weight-averaged progress of non-cancelled tasks, computed in SQL.
- Formula in `database-design.md` §6. No tasks → shown as "—" with tooltip "No tasks yet".

**C7 · Progress snapshots** — As management, I see progress history per project.
- Seeded monthly snapshots; a daily background job (or ADM action `POST /api/admin/snapshots/run`) inserts today's snapshot; `UNIQUE(project_id, snapshot_date)` makes reruns idempotent (upsert).
- API: `GET /api/projects/{id}/progress-history`

---

## Epic D — Risks & issues

**D1 · Record risk/issue** — As a PM, I record type, title, owner, probability (1–5), impact (1–5), mitigation, due date and status.
- Score = probability × impact (DB generated column). Score ≥ 15 requires mitigation text (400 otherwise).
- Closing sets `closed_at`. API: `GET/POST /api/projects/{id}/risks`, `PUT /api/risks/{id}`

**D2 · High-exposure view** — As any user, I see open high-exposure items (status open/mitigating, score ≥ 15) across my scope.
- Closed high-score risks never appear here or in KPIs.
- API: `GET /api/risks?highExposure=true&status=open&projectId=&page=`

**D3 · Risk matrix** — As management, I see a 5×5 probability/impact matrix with counts of open items; clicking a cell lists them (drill-down).

---

## Epic E — Change requests & approvals

State machine (server-enforced):

| From | To | Who |
|---|---|---|
| (new) | draft | any member in scope |
| draft | submitted | requester |
| draft / returned | withdrawn | requester |
| submitted | approved / rejected / returned | PM managing the project or ADM — **never the requester**; reason required |
| returned | submitted | requester (after editing) |
| approved / rejected / withdrawn | — | terminal |

Any other transition → 409 `invalid_transition`.

**E1 · Create & submit** — As a project member, I create a CR (title, description, type, cost impact, schedule impact days) and submit it.
- API: `POST /api/projects/{id}/change-requests`, `PUT /api/change-requests/{id}` (draft/returned only), `POST /api/change-requests/{id}/submit`

**E2 · Review queue** — As a PM or ADM, I see submitted CRs I am eligible to decide (excludes my own).
- API: `GET /api/approvals/change-requests?status=submitted&projectId=&page=`

**E3 · Decide** — As an eligible reviewer, I approve, reject or return a CR with a reason, after a confirmation dialog.
- Self-decision → 403 `self_approval_forbidden` (also DB CHECK). CON → 403. Stale version or already decided → 409.
- CR update + **exactly one** `change_request_history` row + audit event + outbox message (on approve) commit atomically.
- API: `POST /api/approvals/change-requests/{id}/decision` `{ decision: approve|reject|return, reason, version }`

**E4 · History** — As anyone in scope, I see the full timeline of a CR (who, when, from→to, reason).
- API: `GET /api/change-requests/{id}/history`

**E5 · Explicit budget link** — As a PM, when an approved CR affects budget, I may create a budget revision that references it (F3). Approval of a CR **never** changes budget or spend by itself.

---

## Epic F — Financial management

**F1 · Budget overview** — As anyone in scope, I see initial approved budget, approved revisions, current approved budget, actual spend, remaining, utilization, FTC, EAC, projected variance and alerts for a project.
- API: `GET /api/projects/{id}/finance/summary?asOf=`

**F2 · Cost categories** — As an ADM, I manage cost categories (create, rename, deactivate; no delete when used).
- API: `GET/POST /api/finance/cost-categories`, `PUT /api/finance/cost-categories/{id}`

**F3 · Request budget revision** — As a PM, I request a dated revision (positive or negative amount) with rationale, optionally linked to an approved CR of the same project.
- Linked CR must be `approved` and same project (400 otherwise). A reduction that would make current budget negative → 400.
- API: `GET/POST /api/projects/{id}/budget-revisions`

**F4 · Decide budget revision** — As an ADM, I approve or reject a pending revision with reason; never my own request.
- Only approved revisions with `effective_date <= asOf` count. Audit event in same transaction.
- API: `POST /api/finance/budget-revisions/{id}/decision`

**F5 · Record draft cost** — As a PM, I add a cost: project, category, date, amount (> 0), description, optional vendor/reference.
- Requires `Idempotency-Key` header; repeating the same key returns the original record (no duplicate).
- Drafts can be edited or deleted (audited). CON → 403.
- API: `GET /api/projects/{id}/costs?status=&categoryId=&from=&to=&page=`, `POST /api/projects/{id}/costs`, `PUT /api/finance/costs/{id}`, `DELETE /api/finance/costs/{id}` (draft only)

**F6 · Post cost** — As a PM or ADM, I post a draft after a confirmation dialog so it counts toward actual spend.
- Status, `posted_by_id`, `posted_at`, audit event and (if an alert threshold is newly crossed) an outbox message commit atomically.
- Already posted → 409. CON → 403. After posting, summary and dashboard recompute from SQL.
- API: `POST /api/finance/costs/{id}/post` `{ version }`

**F7 · Reverse posted cost** — As a PM or ADM, I correct a posted cost by creating a posted reversal (negative amount, linked) with a reason. Posted rows are never edited or deleted (DB trigger).
- Only one reversal per transaction (409 on second).
- API: `POST /api/finance/costs/{id}/reverse` `{ reason }`

**F8 · Monthly forecasts** — As a PM, I edit a grid of future months × categories for my project.
- Months must be first-of-month and after the reporting month; amounts ≥ 0; saved as upsert in one transaction.
- API: `GET /api/projects/{id}/forecasts`, `PUT /api/projects/{id}/forecasts` (bulk)

**F9 · Spending analysis** — As anyone in scope, I see spend by category and monthly trend per project, from SQL.
- API: `GET /api/projects/{id}/finance/by-category?asOf=`, `GET /api/projects/{id}/finance/monthly?asOf=`

**F10 · Alerts** — As management, I see budget alerts: near budget (utilization 90–100%), overspent (actual > budget), projected overrun (EAC > budget), no forecast.
- Shown on project details, portfolio list badge and dashboard.

**F11 · Financial drill-down** — As management, from any financial KPI I open the exact posted transactions behind it (same filters, same cutoff); their sum equals the KPI.
- API: `GET /api/dashboard/drilldown/costs?{filters}&asOf=&page=`

---

## Epic G — Executive dashboard

Shared filters (sent on every dashboard call): `departmentId`, `projectId`, `managerId`, `status`, `from`/`to` (timeframe), `categoryId` (finance visuals only), `asOf`.

**G1 · KPI cards** — As management, I see:
- Delivery: total projects, active projects, portfolio progress %, overdue incomplete tasks, milestones due ≤ 14 days
- Risk & governance: open high-exposure risks, blocked tasks, pending CRs
- Finance: current approved budget, actual spend, remaining, utilization %, EAC, projected overrun (count + AED)
- Units always visible (AED, %). API: `GET /api/dashboard/kpis?{filters}`

**G2 · Visuals (≥ 5, interactive)**
1. Budget vs Actual vs EAC by project (grouped bar) — `GET /api/dashboard/finance/by-project`
2. Monthly cost trend, actual + forecast (line) — `GET /api/dashboard/finance/monthly`
3. Spend by category (bar/donut) — `GET /api/dashboard/finance/by-category`
4. Overdue tasks by owner (bar) — `GET /api/dashboard/delivery/overdue-by-owner`
5. Risk matrix 5×5 (heatmap) — `GET /api/dashboard/risks/matrix`
6. Project health / progress (table or bar) — `GET /api/dashboard/delivery/project-health`
7. Milestone roadmap (timeline, optional) — `GET /api/dashboard/delivery/milestones`

**G3 · Coordinated filters** — Changing any filter refetches every widget; filter state lives in the URL query string (shareable, survives reload). Category filter disables itself on non-finance visuals with a tooltip.

**G4 · Drill-downs (≥ 2)**
- Finance: click "Actual spend" or a bar in visual 1/2/3 → transaction list (F11).
- Delivery: click "Overdue tasks" or a bar in visual 4 → filtered task list `GET /api/dashboard/drilldown/tasks?overdue=true&{filters}`.
- Risk: click a matrix cell → risk list `GET /api/dashboard/drilldown/risks?probability=&impact=&{filters}`.

**G5 · Live recomputation** — After a task progress update (C2) or a cost posting (F6), returning to / refreshing the dashboard shows new values computed by SQL. No KPI is ever patched in the browser.

**G6 · States & responsiveness** — Each widget has loading skeleton, empty state ("No data for these filters") and error state (message + retry + correlation id). Charts resize; cards stack on mobile.

**G7 · Explainability** — A "How is this calculated?" tooltip on each KPI states the source tables and formula (from `database-design.md` §6).

---

## Epic H — Business automation

**H1 · Approved change notification** — When a CR is approved, the requester and project owner receive an in-app notification.
- Trigger: outbox message `ChangeRequestApproved` with `dedup_key = cr-approved:{id}` written in the decision transaction.
- Worker (`OutboxProcessor` BackgroundService, every 10 s): locks pending messages (`FOR UPDATE SKIP LOCKED`), creates notifications (`ON CONFLICT DO NOTHING` on `(source_outbox_id, user_id)`), marks processed, writes an `automation_runs` row.
- Failure: attempts + 1, exponential backoff, `failed` after 5 attempts with `last_error`; ADM can retry.
- Visible outcome: bell icon count + notifications page.

**H2 · Budget alert** — When posting a cost newly crosses 90% utilization or 100% (overspent), an outbox message `BudgetThresholdCrossed` (`dedup_key = budget:{projectId}:{threshold}`) notifies the project owner once.

**H3 · Automation monitor** — As an ADM, I see automation runs and outbox messages (status, attempts, errors) and can retry failed ones.
- API: `GET /api/admin/automation/runs`, `GET /api/admin/automation/outbox?status=`, `POST /api/admin/automation/outbox/{id}/retry`

---

## Epic I — Data generation & import

**I1 · Reproducible generator** — As the developer, I run `dotnet run --project tools/DataGenerator -- generate --seed 20260930 --out data/csv` and get identical CSVs every time.
**I2 · Edge cases guaranteed** — The generator asserts every edge case in `database-design.md` §10 and exits non-zero if one is missing.
**I3 · Import** — `scripts/import-data.sh` (and `.ps1`) applies migrations, `COPY`s CSVs in FK order, resets identity sequences, creates Identity users with `DEMO_USER_PASSWORD`, and refreshes `reporting_settings`.
**I4 · Data dictionary** — `docs/data-dictionary.md` generated from / aligned with `database-design.md` §3.

---

## Epic J — Reporting track: SQL & Power BI

**J1 · SQL queries** — `sql/reports/01_overdue_tasks.sql`, `02_project_financial_performance.sql`, `03_open_high_exposure_risks.sql`, `04_period_spend_vs_forecast_and_milestones.sql`, each commented.
**J2 · Power BI model** — Connects to PostgreSQL (not CSVs), imports `dim_date`, dims and `vw_*` facts; star schema; single-direction relationships; date table marked.
**J3 · DAX measures (≥ 5)** — Overdue Tasks, Portfolio Progress %, Current Approved Budget, Actual Spend, Budget Utilization %, EAC, Projected Variance.
**J4 · Management page** — Slicers (department, project, manager, period), KPI cards, budget vs actual vs EAC, monthly trend, overdue by owner.
**J5 · Reconciliation** — `docs/kpi-reconciliation.md`: for asOf 2026-09-30 and one filter scope, a table comparing SQL / web dashboard / Power BI for at least: actual spend, current approved budget, overdue tasks, open high-exposure risks; with explanation of any difference.

---

## Epic K — Quality, reliability & delivery

**K1 · Structured errors** — All errors are RFC 7807 `ProblemDetails` with `type`, `title`, `status`, `detail`, `errors` (validation), `code` (business rule) and `correlationId`.
**K2 · Logging & correlation** — Middleware reads/creates `X-Correlation-Id`, adds it to the Serilog log context and the response header. Unhandled exceptions are logged once with the id; the UI shows it on error states.
**K3 · Concurrency** — Optimistic `version` on task, project, CR, cost and revision updates; demo with two tabs → 409 + refresh prompt.
**K4 · Automated checks (≥ 5, target ~15)** — at minimum:
1. Contributor → `POST .../decision` returns 403
2. Contributor → `POST /api/finance/costs/{id}/post` returns 403
3. Requester cannot approve own CR (403) and DB CHECK rejects it
4. Invalid CR transition (approved → submitted) returns 409
5. Decision writes exactly one history row (and none on failure)
6. Actual spend excludes drafts and post-cutoff costs; reversal nets out
7. Current budget ignores pending/rejected revisions
8. EAC / variance for a project with and without forecast
9. Portfolio spend equals raw SUM (no join fan-out)
10. Drill-down sum equals KPI
11. Out-of-scope project returns 404
12. Same `Idempotency-Key` does not create a duplicate cost
13. Outbox processed twice creates one notification
**K5 · Manual E2E checklist** — `docs/manual-test-checklist.md` mapping acceptance tests A–H below.
**K6 · Repo & setup** — Meaningful commits, `README.md` setup, `.env.example`, `docker compose up`, migration + import commands, demo logins table (passwords via env), Swagger at `/swagger`, `docs/api-routes.md`.
**K7 · Security notes** — `docs/security-notes.md`: secrets via env/user-secrets, password hashing (Identity), JWT validation parameters, refresh cookie flags, CORS allow-list, parameterized SQL only, input validation, 404-vs-403 policy, rate limiting on login.
**K8 · AI development log** — `docs/ai-development-log.md`, ≥ 4 iterations, ≥ 1 case where AI was wrong and how it was found/fixed.
**K9 · Presentation** — 6–8 slides: business value, solution, architecture, finance, web intelligence, automation, AI approach, testing & delivery.

---

## Epic L — Optional: MS Access / VBA

**L1** — `access/PmoUtility.accdb` linked via ODBC (psqlODBC) to `projects` and `tasks` (read-only views), a form listing overdue tasks by project, a saved query, a report, and a VBA button "Export overdue to CSV" with `On Error` handling. Only if core is done.

---

## Demonstrable business journeys → stories

| # | Journey (assignment §1) | Stories |
|---|---|---|
| 1 | PM creates project, owner, dates, budget | B1, B5 |
| 2 | Team member updates assigned task → progress & dashboard change | C2, C6, G5 |
| 3 | Manager updates milestone or risk with validation | C5, D1 |
| 4 | Authorized user posts cost → spend/remaining/utilization update | F5, F6, F1, G5 |
| 5 | CR submitted and reviewed with history | E1–E4, H1 |
| 6 | Management filters dashboard, drills down to records | G3, G4, F11 |

## Live acceptance tests → stories

| Test | Stories |
|---|---|
| A End-to-end persistence | B1, C1, C2 |
| B Finance mutation | F5, F6, F11 |
| C Approval transaction | E3, E4 |
| D Authorization denial | A3, K4 #1–2 |
| E Dashboard integrity | G1–G5, §6 SQL |
| F Analytics agreement | J5 |
| G Automation / VBA | H1, H3, L1 |
| H Engineering quality | K1–K4 |

## Suggested build order (vertical slices)

1. Solution skeleton, Docker Compose, DB, migrations for all tables, correlation/logging, ProblemDetails
2. Data generator + import (I1–I3) — real data early makes every later slice demoable
3. Auth + scope (A1–A3)
4. Projects (B1–B4, B6)
5. Tasks & milestones (C1–C6)
6. Finance (F1, F5–F7, then F3–F4, F8–F11)
7. Change requests (E1–E4) + outbox automation (H1–H3)
8. Risks (D1–D3)
9. Dashboard (G1–G7)
10. Tests hardening (K4), docs (K5–K8)
11. Power BI (J1–J5), slides (K9)
12. Access (L1) only if time remains
