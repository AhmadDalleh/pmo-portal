# 07 — Executive dashboard (G1–G7, F11)

## 1. Business goal
Management needs one page that answers "are we on time, on budget and under control?" for the projects they are allowed to see.
Every number must be reproducible and explainable (formula tooltip) and must be traceable to the records behind it (drill-down).
Filters change every widget at once, and the view is shareable as a link.

## 2. UI screens and actions (`/dashboard`)
| Area | Click | Result |
|---|---|---|
| Filter bar | Department, Project, Project manager, Status, Period from/to, As of, Cost category; **Clear filters** | The URL query string changes (e.g. `?managerId=3`); **all** widgets refetch. Copy the URL to another tab: same view. Back button works. |
| Delivery cards | Hover (i); click **Overdue tasks** | Formula text; drill-down to the overdue task list. Cards: projects (+active), portfolio progress, overdue tasks, milestones due in 14 days. |
| Risk & governance cards | Click **Open high-exposure risks** | Risk drill-down. Blocked tasks, pending change requests. |
| Finance cards (AED) | Click **Actual spend** | `/dashboard/drilldown/costs`: the posted transactions; page shows "total … matches the Actual spend card". Cards: budget, spend, remaining, budget used, EAC (+ forecast), projected overrun (count + AED). |
| Charts | Click a bar / slice / cell | Budget vs actual vs EAC (bar → costs of that project); monthly trend (no click); spend by category (slice → costs of that category); overdue by owner (bar → that person's tasks, or Unassigned); 5×5 risk matrix (cell → risks). |
| Project health table | Click a row | Opens the project; badge = same rule as the project page. |
| Each widget | Force an error (stop the API) | Skeleton while loading, message + reference id + **Retry**; empty: "No data for these filters." |

## 3. Per action
| Action | Route | Chain | Rules | Tables |
|---|---|---|---|---|
| KPI cards | `GET /api/dashboard/kpis?departmentId&projectId&managerId&status&from&to&asOf` | `DashboardController` → `DashboardService.KpisAsync` → scope ids + as-of → `DashboardReadModel` → `WITH fp AS (filtered_projects.sql) …` + `per_project_finance.sql` + `kpis.sql` | scope: `p.id = ANY(@scope_ids)`; §6 definitions; portfolio progress is task-weighted | `projects, tasks, milestones, risks, change_requests, project_budgets, budget_revisions, cost_transactions, financial_forecasts` (each in its own CTE: no join fan-out) |
| Charts | `…/finance/by-project`, `…/finance/monthly`, `…/finance/by-category`, `…/delivery/overdue-by-owner`, `…/risks/matrix` | same chain, one SQL file each | category filter only on monthly, by-category and cost drill-down | reads |
| Health | `GET …/delivery/project-health` | `DashboardService.ProjectHealthAsync` → `IProjectReadModel.SearchAsync` + `ProjectHealth` | same filters and same SQL as the project list | reads |
| Drill-downs | `…/drilldown/costs`, `…/drilldown/tasks?overdue&assigneeId&unassigned`, `…/drilldown/risks?probability&impact&highExposure` | same `fp` CTE | **same WHERE as the KPI**; costs returns `totalAmount` of all matching rows | reads |
| Nothing writes | – | – | – | no transactions |

Filter semantics: `managerId` = project owner; `from/to` = projects whose start..planned end overlaps the window; `asOf` default `Reporting:DefaultAsOf` (2026-09-30).

## 4. Test scenarios (fresh seed)
| Type | User | Steps | Expected |
|---|---|---|---|
| Happy | `admin1` | Open the dashboard, filter **Project manager = Chanelle Stamm (pm1)** | Projects 3 (2 active); budget **AED 2,850,000**; actual spend **2,175,500**; remaining 674,500; budget used **76.3%**; EAC **2,525,500** (forecast 350,000); projected overrun **1 project, AED 109,500** (PRJ-009). |
| Drill-down = KPI | same | Click **Actual spend** | Total at the top = AED 2,175,500.00 and "matches the Actual spend card"; rows only from PRJ-001/006/009, none after 2026-09-30, no drafts. |
| Filters in URL | any | Select Status = On hold | URL gets `?status=on_hold`; only PRJ-006 counted; reload keeps it; "Clear filters" empties it. |
| Category filter | `admin1` | Cost category = Software Licences | Monthly trend, category chart and the cost list shrink; KPI cards do **not** change (they say so in the tooltip text). |
| Validation | `admin1` | Swagger `GET /api/dashboard/kpis?status=lost` and `?from=2026-12-01&to=2026-01-01` | 400 `validation_failed` (field errors). |
| Scope (forbidden data) | `con1` | Dashboard, then `?projectId=2` | Projects = number of con1 memberships (4 in the fresh seed); with `projectId=2` all numbers are 0 (no leak, no 403). Anonymous → 401. |
| Seed edge: overdue | `admin1` | Overdue tasks card | **8** overdue (fresh seed); bar chart by owner sums to 8; drill-down lists 8 rows; due-today task #85 is **not** included. `?asOf=2026-10-01` → 9. |
| Seed edge: post-cutoff cost | `admin1` | `?asOf=2026-10-31` | Actual spend rises by AED 30,000 (PRJ-009 cost #122); default date excludes it. |
| Seed edge: forecast overrun / no forecast | `admin1` | Budget vs actual vs EAC chart | PRJ-002 EAC bar above its budget; PRJ-005 EAC equals actual (no forecast); PRJ-009 spend above budget. |
| Seed edge: no tasks | `admin1` | Health table, PRJ-010 | Progress "No tasks". |
| Live recomputation | `pm3` then `admin1` | Post PRJ-005 draft #66 (module 04), reload dashboard with `?projectId=5` | Spend 265,551; drill-down total equals it. No browser-side patching. |
| Mobile | any | Narrow the window | Cards stack, charts resize. |

## 5. SQL to verify (independent of the app code)
```sql
-- KPIs for pm1's projects (ids 1, 6, 9) as of 2026-09-30
select (select sum(amount) from cost_transactions where status='posted' and txn_date <= '2026-09-30' and project_id in (1,6,9)) as actual_spend;                 -- 2175500
select (select sum(initial_amount) from project_budgets where project_id in (1,6,9))
     + (select coalesce(sum(amount),0) from budget_revisions where status='approved' and effective_date <= '2026-09-30' and project_id in (1,6,9)) as budget;    -- 2850000
select sum(amount) from financial_forecasts where period_month > '2026-09-01' and project_id in (1,6,9);                                                          -- 350000
-- overdue tasks (whole database)
select count(*) from tasks where status not in ('done','cancelled') and due_date < '2026-09-30';                                                                   -- 8
-- portfolio progress (task weighted)
select sum(weight*progress_pct)::numeric/sum(weight) from tasks where status <> 'cancelled';
-- the fan-out trap: this wrong query inflates spend (do NOT use), compare with the right one
select sum(c.amount) from projects p join tasks t on t.project_id=p.id join cost_transactions c on c.project_id=p.id where c.status='posted' and c.txn_date <= '2026-09-30';
select sum(amount) from cost_transactions where status='posted' and txn_date <= '2026-09-30';
```

## 6. Interview questions
1. **How do you guarantee the drill-down equals the KPI?** Every query starts from the same `fp` CTE and parameters; the drill-down returns the sum of all matching rows and the page compares it with the KPI; a parameterised test covers five filter sets.
2. **What is the join fan-out trap and how is it avoided?** Joining tasks and costs to a project multiplies rows; each one-to-many table is aggregated per project in its own CTE first (§7), and a test compares the dashboard to a plain `SUM`.
3. **Why are the filters in the URL?** Shareable links, back button and reload; one source of truth read with `toSignal(queryParamMap)`; an `effect` refetches every widget.
4. **Why no KPI math in Angular?** One definition in SQL shared by API, reports and Power BI; the client only formats numbers.
5. **How is data scope enforced on the dashboard?** The service passes the caller's accessible project ids into the SQL (`p.id = ANY(@scope_ids)`); a filter for an out-of-scope project yields zeros, never someone else's data.
