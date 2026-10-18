# Where the code differs from the docs

Read this before the interview: an interviewer who reads `docs/` and then your code may ask about each line below. "Doc" = `user-stories.md`, `database-design.md`, `CLAUDE.md`. "Code" = what the repository actually does today (checked in the source, not in the docs).

## A. Behaviour that differs from a written requirement

| # | Doc says | Code does | Where | What to say |
|---|---|---|---|---|
| 1 | A4: an administrator cannot remove **their own Administrator role**. | Cannot change **their own role at all** (any change), and cannot deactivate themselves. | `UserAdminService.ChangeRoleAsync / SetActiveAsync` | Stricter, same purpose (no lock-out). |
| 2 | A2: `/api/auth/me` returns **roles + managed project ids**. | Returns **one** `role` string (the model is one role per user) + `managedProjectIds`. | `MeDto`, `AuthController.Me` | Matches design doc ("one role per user"); the wording in A2 was loose. |
| 3 | H1: worker "writes an `automation_runs` row" per run. | A run row is written **only when a batch had messages**. Idle polls write nothing (otherwise 8,640 empty rows a day). | `OutboxBatchRunner.RunOnceAsync` | Deliberate; say so. |
| 4 | C7: a daily job (or ADM action) inserts **today's** snapshot. | The daily job snapshots `Reporting:DefaultAsOf` (2026-09-30 in the demo), not the system date; runs once per UTC day, checked hourly; ADM action takes an optional `date`. | `DailySnapshotJob`, `SnapshotService.RunAsync` | In production `DefaultAsOf` would be "today". |
| 5 | §10: only **PRJ-005** has no forecast data. | PRJ-004 (completed, ended in July) has no future forecast rows either, so `no_forecast` is also true for it. | `ForecastSeeder` | Harmless; the completed project needs none. |
| 6 | §10 lists forecasts "Oct-2026 → project end". | Exactly that. No September rows exist, so the "current month is excluded" boundary is tested with `asOf`, not with a seed row. | `ForecastSeeder` | Fine. |
| 7 | B3: filter by "date range". | Keeps projects whose **start..planned end overlaps** `[from, to]`. Dashboard uses the same meaning and does **not** clip individual transactions. | `ProjectPortfolio.sql`, `filtered_projects.sql` | Chosen so drill-down sum = KPI. |
| 8 | G3: category filter "disables itself" on non-finance visuals with a tooltip. | The filter stays enabled; each chart card says "Category filter does not apply" / "Respects the category filter", and the server ignores `categoryId` for KPIs, budget-vs-actual, overdue, matrix, health. | `DashboardApi`, `chart-card` notes | Same effect, different UI. |
| 9 | G2 visual 7 (milestone roadmap) optional. | **Not built.** | – | Optional per brief. |
| 10 | D3: risk matrix page with clickable cells. | The matrix is a **dashboard visual** only; cells drill down to `/dashboard/drilldown/risks`. There is no separate matrix page. The API `GET /api/risks` exists but no UI screen lists risks across projects. | `dashboard-page.ts` | Say "D2 is API-only; D3 is on the dashboard". |
| 11 | F2: admin manages cost categories. | Done in slice 09: `/admin/categories` (create, rename, deactivate). | `admin-categories.ts` | Resolved. |
| 12 | A4, A5, H3: admin screens. | Done in slice 09: `/admin/users`, `/admin/audit`, `/admin/automation`; users are filtered and paged in the browser because the API returns all users. | `features/admin/` | Resolved. |
| 13 | `CLAUDE.md` frontend: shared `form-field` wrappers. | Not built; forms use Angular Material fields directly with `fieldError()` / `applyServerErrors()` helpers. | `shared/notify.ts` | Simplification. |
| 14 | F7 does not say which date a reversal carries. | The reversal has the **same `txn_date` as the original**, so they net out in every period (a reversal dated today would not net at asOf 2026-09-30). | `CostTransaction.Reverse` | Decision documented in slice 05. |
| 15 | F3: "A reduction that would make current budget negative → 400". | Checked against **initial + all approved revisions** (any date) + the new amount, i.e. conservative, not "budget at the effective date". | `BudgetRevisionService.RequestAsync` | Mention if asked. |
| 16 | F6: an outbox message when a threshold is "newly crossed". | Evaluated at the **reporting date** (`DefaultAsOf`) using spend/budget before and after the posting; each `budget:{project}:{90 or 100}` key can be queued **once ever** (UNIQUE dedup key). | `CostService.QueueThresholdMessagesAsync` | A project that already crossed 90% in the seed (PRJ-001, PRJ-009) will never queue 90 again. |
| 17 | C2: contributors update progress and status. | A contributor **cannot set or leave `cancelled`** (403); only a manager can cancel or reopen. | `TaskService.UpdateProgressAsync` | My addition: cancelling removes work from project progress. |
| 18 | B5: remove a member with open tasks → 409 "listing those tasks". | The 409 `detail` lists `#id title` of the tasks in the message text (not a separate field). | `ProjectMemberService.RemoveAsync` | OK. |
| 19 | E2: the review queue excludes "my own". | Also true for administrators (their own requests are excluded from their queue). The administrator can still decide a PM's own request when it appears in their queue. | `ChangeRequestService.ReviewQueueAsync` | OK. |
| 20 | K7: "rate limiting on login". | Fixed window per IP, default 20/min, config `RateLimiting:LoginPerMinute`. | `Program.cs` | Plus account lockout. |

## B. Documents the code makes stale or that were never written

| Doc | Status |
|---|---|
| `database-design.md` §9 reporting views and `dim_date` | Created by migration `ReportingViews`; `sql/reports/` exists. `dim_date` is a view, not a table. |
| `docs/data-dictionary.md` (I4) | Written: per-CSV column list generated from the schema. |
| `docs/kpi-reconciliation.md` (J5) | Written as `docs/reconciliation.md`; SQL and web columns filled, Power BI column pending the report. |
| `README` / `CLAUDE.md` "docker compose up" for api + web | Compose runs **only the database**; no Dockerfiles for api/web. |
| `CLAUDE.md` Commands | Updated during the build (port 5433, `Jwt:Key`). Everything listed there works. |
| Design doc §3 refresh tokens | Updated: refresh tokens live in Identity `user_tokens` (no extra table). |
| Design doc §3 risks: `Risk:HighExposureThreshold` configurable | It is a **constant** (`Risk.HighExposureThreshold = 15`) in the domain and a literal `15` in SQL. |

## C. Things that are true in code but easy to forget

- **Seed audit actions equal the API's** (`change_request.decided`, `budget_revision.decided`, …). They were different in the first generator version; fixed in slice 6.
- **Snapshots** count non-cancelled tasks (`tasks_total`); the seed and `SnapshotRunner` use the same definition.
- **Login is not one transaction**: the failed-attempt counter, the audit row and the refresh-token row are separate commits (see module 01).
- **Optimistic concurrency uses `xmin`** (PostgreSQL system column). It is only checked when the request carries a `version`; endpoints without `version` in the body (add member, forecast save, notification read, category update) are last-write-wins.
- **Forecast save** is last-write-wins: `financial_forecasts` has no version column.
- **Timestamps vs business dates:** a milestone completed "today" uses the **system clock date** (not `asOf`), so on a machine whose clock is after 2026-09-30 `completed_at` can be later than the reporting date.
- **Local database state:** after you click around, the database no longer equals the seed. Re-import (`./scripts/import-data.sh`) before the interview or any demo that quotes exact numbers.
