# KPI reconciliation: SQL vs web dashboard vs Power BI (assignment F)

Snapshot: reporting date 2026-09-30, no filters, administrator scope (all 10 projects), freshly reset seed (`scripts/reset-db`).
SQL = queries on the base tables; Web = `GET /api/dashboard/kpis` (the dashboard KPI cards); Power BI = the measure in `powerbi/measures.dax`.
The first two columns were measured on 2026-10-18. **Fill the Power BI column from the finished report** and tick the last column.

| # | KPI | SQL (base tables) | Web dashboard | Power BI measure | Power BI value | Match |
|---|---|---|---|---|---|---|
| 1 | Approved budget (AED) | 12,020,000.00 | 12,020,000.00 | `[Approved Budget]` | | [ ] |
| 2 | Actual spend (AED) | 6,556,000.00 | 6,556,000.00 | `[Actual Spend]` | | [ ] |
| 3 | Budget utilization | 54.54 % | 54.54 % (0.5454) | `[Budget Utilization %]` | | [ ] |
| 4 | EAC (AED) | 10,706,000.00 | 10,706,000.00 | `[EAC]` | | [ ] |
| 5 | Overdue tasks | 8 | 8 | `[Overdue Tasks]` | | [ ] |
| 6 | Open high-exposure risks | 4 | 4 | `[Open High Exposure Risks]` | | [ ] |
| 7 | Task progress | 63.24 % | 63.24 % | `[Task Progress %]` | | [ ] |

Also equal in SQL and web: remaining budget 5,464,000.00; forecast-to-complete 4,150,000.00; projected variance 1,314,000.00; projected overrun projects 2; milestones due within 14 days 5; blocked tasks 4; pending change requests 4.

## How each SQL value is obtained
```sql
-- 1 approved budget
SELECT SUM(pb.initial_amount) + COALESCE((SELECT SUM(amount) FROM budget_revisions
       WHERE status = 'approved' AND effective_date <= '2026-09-30'), 0) FROM project_budgets pb;
-- 2 actual spend
SELECT SUM(amount) FROM cost_transactions WHERE status = 'posted' AND txn_date <= '2026-09-30';
-- 3 utilization = 2 / 1
-- 4 EAC = actual spend + forecast months after September 2026
SELECT (SELECT SUM(amount) FROM cost_transactions WHERE status = 'posted' AND txn_date <= '2026-09-30')
     + (SELECT SUM(amount) FROM financial_forecasts WHERE period_month > '2026-09-01');
-- 5 overdue tasks
SELECT count(*) FROM tasks WHERE status NOT IN ('done','cancelled') AND due_date < '2026-09-30';
-- 6 open high-exposure risks
SELECT count(*) FROM risks WHERE status IN ('open','mitigating') AND score >= 15;
-- 7 task progress (cancelled tasks excluded, weighted by task weight)
SELECT SUM(weight * progress_pct)::numeric / SUM(weight) FROM tasks WHERE status <> 'cancelled';
```
The reporting views give the same numbers (`vw_project_financials`, `vw_task_facts`, `vw_risk_facts`); `ReportingViewTests` asserts it against the live dashboard API on every test run, so the SQL and web columns cannot drift apart.

## Expected differences and how to explain them
| Situation | Why it differs | What to say |
|---|---|---|
| Power BI shows older numbers | Import mode keeps data from the last Refresh | Click Refresh after `reset-db` or after changing data in the app |
| Reporting date | Web dashboard accepts `asOf`; the views read `reporting_settings.as_of_date` | Both are 2026-09-30 for the baseline. Production: set the views' date to today and let the dashboard default to today |
| Category slicer in Power BI changes spend but not the budget | Category exists only on cost and forecast rows; budgets are per project | Same rule as the web dashboard: category applies to compatible finance visuals only |
| Utilization 54.5 % vs 0.5454 | Format only | Web returns a ratio, Power BI formats it as % |
| Task progress | Web returns 63.24 (percent); the DAX measure divides by 100 for % formatting | Same value |
| Contributor or manager scope | Web dashboard is scope-aware (member projects only); Power BI connects with its own database login | The reconciliation uses the administrator scope; Power BI is a management report without row-level security |
| Spend by category total | Uses `vw_cost_facts`; equals actual spend only when no category filter is set | Total with no filter equals KPI 2 |
