# Manual test checklist (acceptance tests A-H)

Setup: seed imported, API and web running, reporting date 2026-09-30. Each item names the automated test that covers the same rule.

## A. End-to-end persistence (B1, C1, C2)
- [ ] As `pm5`, create a project (Projects → New project). It appears in the list; reload the page: still there. SQL: `select * from projects order by id desc limit 1; select * from project_budgets order by id desc limit 1;`
- [ ] Add a task assigned to `con1` (add `con1` as member first). Sign in as `con1` → My work → update progress to 60%. Reload: 60%.
- Automated: `ProjectWriteTests`, `TaskWriteTests.Progress_ByAssignee_ChangesProjectProgressOnNextRead`.

## B. Finance mutation (F5, F6, F11)
- [ ] As `pm3`, PRJ-005 → Finance: new draft cost 50,000 dated 2026-09-20; summary spend unchanged (248,000).
- [ ] Post it (confirm dialog): spend 298,000, utilisation 48.1%; dashboard drill-down total equals the Actual spend card.
- [ ] Try Edit/Delete on the posted row: not offered; reverse it: spend back to 248,000.
- Automated: `CostTests`, `DashboardTests.CostDrilldown_*`.

## C. Approval transaction (E3, E4)
- [ ] As `con1`, PRJ-001 → Change requests → New → Submit. As `pm1` → Approvals → Approve with a reason.
- [ ] Open the request: timeline shows draft → submitted → approved with names and reason. SQL: 3 rows in `change_request_history`, one `outbox_messages` row `cr-approved:<id>`.
- [ ] Within ~10 s `con1` has a notification (bell count 1).
- Automated: `ChangeRequestTests`, `OutboxTests`.

## D. Authorization denial (A3, K4 #1-2, #11)
- [ ] As `con1`: no Approvals menu; `POST /api/approvals/change-requests/{id}/decision` in Swagger → 403; `POST /api/finance/costs/{id}/post` → 403.
- [ ] As `con1`: `GET /api/projects/2` (not a member) → 404.
- [ ] As `pm1` approve your own request → 403 `self_approval_forbidden`.
- Automated: `ChangeRequestTests.Decide_AsContributor_*`, `CostTests.Post_AsContributor_*`, `ProjectReadTests.Get_ProjectOutsideScope_Returns404`.

## E. Dashboard integrity (G1-G5)
- [ ] Unfiltered KPIs vs SQL in `docs/database-design.md` §6; filter by manager `pm1`: budget 2,850,000, spend 2,175,500, EAC 2,525,500.
- [ ] Change a filter: every widget updates, URL changes; copy the URL into a new tab: same view.
- [ ] After posting a cost or finishing a task, the dashboard shows new values on the next load.
- Automated: `DashboardTests`.

## F. Analytics agreement (J5)
- SQL vs web: `DashboardTests.Kpis_Unfiltered_MatchIndependentSqlOverTheWholeDatabase` and `ReportingViewTests`. Power BI column: fill `docs/reconciliation.md` after building the report (`powerbi/README.md`).

## G. Automation (H1, H3)
- [ ] As `admin1`: `GET /api/admin/automation/outbox?status=failed` shows the seeded failed alert; `POST .../retry` returns it to pending; the worker processes it.
- Automated: `OutboxTests`.

## H. Engineering quality (K1-K3)
- [ ] Two browser tabs on the same project: save in tab 1, save the stale form in tab 2 → 409 message, no overwrite.
- [ ] Trigger a validation error (end before start): field message under the field; a 403/409 shows the server text with a reference id.
- [ ] Double-click Save on a new cost: one row (Idempotency-Key). SQL: `select count(*) from cost_transactions where idempotency_key = '<key>'`.
- Automated: `ProjectWriteTests.Update_WithStaleVersion_*`, `CostTests.CreateDraft_SameIdempotencyKeyTwice_*`.

## K4 coverage map (13 required checks)
1 `ChangeRequestTests.Decide_AsContributor_Returns403_AndNothingChanges` · 2 `CostTests.Post_AsContributor_Returns403AndCostStaysDraft` · 3 `Decide_OwnRequest_Returns403_AndTheDatabaseCheckRejectsItToo` · 4 `InvalidTransitions_Return409` · 5 `Approve_WritesExactlyOneHistoryRow...` and `Decide_WhenTheOutboxInsertFails_RollsBack...` · 6 `CostTests.Spend_ExcludesDraftsAndPostCutoffCosts_AndAReversalNetsOut` · 7 `RevisionForecastCategoryTests.PendingRevision_*`/`RejectedRevision_*` · 8 `Forecasts_WithoutRows_*` and `FinanceReadTests.Summary_WithAndWithoutForecast_*` · 9 `DashboardTests.FinanceCharts_AddUpToTheKpis` · 10 `CostDrilldown_SumsToTheActualSpendKpi_ForEveryFilter` · 11 `ProjectReadTests.Get_ProjectOutsideScope_Returns404` · 12 `CostTests.CreateDraft_SameIdempotencyKeyTwice_*` · 13 `OutboxTests.Worker_ProcessingTheSameMessageTwice_*`.
