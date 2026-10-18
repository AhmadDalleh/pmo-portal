# Slice 07 — Dashboard analytics and the Angular app (G1–G7, F11, UI for A–F)

## What changed
- **Backend** (`Pmo.Application/Features/Dashboard`, `Pmo.Infrastructure/Analytics/Dashboard/*.sql`, `DashboardReadModel`, `DashboardController`): 7 KPI/chart endpoints and 3 drill-downs. Tests: `DashboardTests` (19 cases).
- **Frontend** (`src/frontend/pmo-web`, Angular 22, standalone components, signals, Angular Material, ngx-echarts):
  - `core/` — models, one API client per resource, auth service (token in memory, refresh cookie), interceptors (error → correlation id → auth).
  - `shared/` — `data-table`, `async-state` (loading/empty/error + retry), `confirm-dialog`, `status-badge`, `kpi-card`, `chart-card`, money/percent pipes, `notify` (toasts + server field errors).
  - `features/` — login, projects (list, create, shell with tabs, overview, members), tasks (list, my work, milestones, dialogs), finance (summary, charts, costs, revisions, forecast grid), risks, change requests (project tab, page with timeline, approvals queue), notifications, dashboard + drill-down.

## Request flow (click "Actual spend" on the dashboard)
`DashboardPage` KPI card → router navigates to `/dashboard/drilldown/costs?{same filters}` → `DrilldownPage` → `DashboardApi.costs` → `GET /api/dashboard/drilldown/costs` → `DashboardController` → `DashboardService` (scope ids, as-of) → `DashboardReadModel` → `WITH fp AS (filtered_projects.sql) ... drilldown_costs.sql` → `cost_transactions`. The page also loads the KPI with the same filters and shows "matches the Actual spend card".

## Key decisions
- **One filtered-project CTE** (`filtered_projects.sql`) starts every dashboard query. KPI, chart and drill-down therefore cannot disagree about which projects they cover; the tests prove drill-down sums equal the KPI for five different filter sets.
- **Filters live in the URL.** `toSignal(queryParamMap)` is the only filter state; an `effect` refetches all widgets. Shareable, back button works, reload keeps state.
- **Category filter** affects only the monthly trend, spend by category and cost drill-down (documented in the filter bar tooltip and chart notes). KPI cards and budget-vs-actual ignore it because a budget has no category.
- **Timeframe** keeps projects whose start..planned-end overlaps the window; it does not clip individual transactions (so the drill-down sum stays equal to the KPI).
- **No KPI maths in TypeScript.** The only client-side arithmetic is the display total of a forecast column while typing and a risk-score preview; both are labelled and the server value replaces them after saving.
- **Token in memory, cookie for refresh.** A reload calls `/api/auth/refresh` at startup (`provideAppInitializer`); one 401 triggers one refresh and one retry.
- **Server rules mirrored, not trusted**: forms validate required, ranges and date order for fast feedback; `ProblemDetails.errors` are mapped onto fields; 403/409 show the server text.
- **Milestone roadmap (optional visual 7) and admin screens were not built** (backend admin endpoints exist).

## Rules and where they are enforced
| Rule | Where |
|---|---|
| Dashboard numbers limited to caller scope; out-of-scope `projectId` yields zeros | `DashboardService` passes scope ids; SQL `p.id = ANY(@scope_ids)` |
| KPI definitions | `kpis.sql`, `per_project_finance.sql` (database-design §6) |
| Drill-down sum = KPI | shared `fp` CTE; tests `CostDrilldown_SumsToTheActualSpendKpi_ForEveryFilter` |
| Contributors see no Approvals menu / buttons | cosmetic (`AuthService.isManager/canManage`); API refuses anyway |
| Posting/reversing/deciding ask for confirmation | `Confirm` service before every consequential call |

## Transaction behaviour
Dashboard is read-only. The UI refetches after every mutation (`load()` after post, reverse, revision, forecast save, task progress); nothing is patched locally.

## How to demo (2 min)
1. `docker compose up -d db`, import seed, start API (`dotnet run --project src/backend/Pmo.Api`) and `npm start` in `src/frontend/pmo-web` (proxy to `localhost:5217`).
2. Sign in as `pm1@pmo.example`. Dashboard: budget AED 2,850,000, actual spend AED 2,175,500, EAC AED 2,525,500, 1 projected overrun (AED 109,500).
3. Click "Actual spend": the list total equals the card. Go back, set Cost category = Software: only the trend, category chart and cost list change.
4. Click the "Overdue tasks by owner" bar: the task list for that person. Click a risk-matrix cell: the risks in it.
5. Project PRJ-009 → Finance: post the draft cost (confirm dialog), watch spend, utilisation and charts change; sign in as `con1` and note there is no Approvals menu.

## Likely interview questions
1. **How do you guarantee the drill-down matches the KPI?** Same CTE and parameters; the page shows both numbers; a parameterised test checks five filter sets.
2. **Why are the filters in the URL?** Shareable links, back button, reload; one source of truth for every widget.
3. **Why not compute KPIs in Angular?** One definition (SQL, database-design §6) shared with Power BI; the client could otherwise drift.
4. **How does token refresh work in the browser?** Access token in a signal; HttpOnly refresh cookie; interceptor refreshes once on 401 and retries.
5. **What does the category filter do and why not on KPIs?** Applies to finance visuals only; budgets have no category.

## Live-change rehearsal
"Show milestones due in 7 days": change `+ 14` to `+ 7` in `Analytics/Dashboard/kpis.sql` (`milestone_k`) and the card label in `dashboard-page.ts`; update `Kpis_Unfiltered_...` expectation (`'2026-10-14'` to `'2026-10-07'`).
