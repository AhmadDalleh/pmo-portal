# Report queries (assignment section 7)

Four commented queries over the reporting views (`vw_*`, created by migration `ReportingViews`; definitions in `docs/database-design.md` sections 6 and 9). Reporting date 2026-09-30 comes from `reporting_settings`.

| File | Question | Rows at the baseline |
|---|---|---|
| `01_overdue_tasks.sql` | Which incomplete tasks are past due? | 8 (one task due on 2026-09-30 is "due today", not overdue) |
| `02_project_financial_performance.sql` | Budget, spend, EAC and flags per project | 10 (PRJ-009 overspent, PRJ-002 projected overrun, PRJ-001 near budget, PRJ-004 and PRJ-005 no forecast) |
| `03_open_high_exposure_risks.sql` | Open risks/issues with score 15 or more | 4 (a closed high-exposure risk is excluded) |
| `04_period_spend_forecast_and_milestones.sql` | Monthly spend vs forecast; milestone status per project | 26 months; 10 projects |

Run one:
```bash
docker exec -i pmo-db psql -U pmo -d pmo_db < sql/reports/02_project_financial_performance.sql
```
