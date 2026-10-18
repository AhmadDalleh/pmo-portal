# Submission status against Candidate_Assignment.pdf

Audit date 2026-10-18, branch `claude/pmo-feature-slice-dev-39703d` at 6b0de52. No code was changed for this audit.
Checks run: `dotnet test` (51 unit + 190 integration pass), `ng build` (passes), psql counts on `pmo_db` (localhost:5433), code and doc reading.

Legend: DONE / PARTIAL / MISSING / MANUAL (only you can do it). Effort: S under 1 h, M half a day, L a day or more.

## Summary
- **Core build (§1-§5, §6, §10 automated side): DONE.** Backend, finance, authorization, dashboard, generator and automation are implemented and tested.
- **Gaps that cost marks:** Power BI (§7, MISSING), reporting SQL/views (MISSING), Access (optional, MISSING), slides (MISSING), cross-system reconciliation (MISSING), data dictionary and architecture note (PARTIAL), disclosure table in the AI log (PARTIAL), browser verification of most UI flows (MANUAL), Docker images and hosted URL (optional).

## §1 Business journeys

| # | Requirement | Status | Evidence or gap | Owner | Effort |
|---|---|---|---|---|---|
| 1 | PM creates project, owner, dates, initial approved budget | DONE (API) / MANUAL (UI) | `ProjectWriteTests`; `project-form-page.ts`. Not yet clicked through in a browser by you | You | S |
| 2 | Contributor updates task; progress and dashboard change | DONE (API) / MANUAL (UI) | `TaskWriteTests.Progress_ByAssignee_ChangesProjectProgressOnNextRead`; `DashboardTests`. UI flow (`progress-dialog.ts`) never run in a browser | You | S |
| 3 | Manager updates milestone or risk with validation | DONE (API) / MANUAL (UI) | `Tasks/`, `Risks/` integration tests; dialogs exist | You | S |
| 4 | Record and post cost; spend, remaining, utilization update | DONE (API) / MANUAL (UI) | `CostTests`, `FinanceReadTests`; post/reverse UI never run in a browser | You | S |
| 5 | Change request submit and review with history | DONE (API) / MANUAL (UI) | `ChangeRequestTests` (one history row per decision); approvals UI not browser-tested | You | S |
| 6 | Filter dashboard, drill down, trace to DB | DONE | `DashboardTests.CostDrilldown_SumsToTheActualSpendKpi_ForEveryFilter`; dashboard seen in browser earlier | You (rehearse) | S |

## §2 Modules

| Module | Status | Evidence or gap | Owner | Effort |
|---|---|---|---|---|
| 01 Portfolio (create/view/edit, search/filter dept/owner/status/dates, details) | DONE | `ProjectsController`, `ProjectPortfolio.sql`, `projects-page.ts` | - | - |
| 02 Tasks and milestones (assignee, status, priority, weights, overdue/upcoming, progress) | DONE | `TasksController`, `MilestonesController`; weighted progress in domain; `project-tasks.ts` | - | - |
| 03 Risks (score = P x I, open high exposure) | DONE | `RisksController`, `risk_matrix.sql`; seed has 3 open high, 1 closed high | - | - |
| 04 Change requests (transitions, self-approval block, history) | DONE | `ChangeRequestTests` (K4 #1, #3, #4, #5); append-only history trigger | - | - |
| 05 Finance (budgets, revisions, categories, costs, forecasts, summary, alerts) | DONE | `FinanceController`, `CostsController`; `RevisionForecastCategoryTests`; alert = outbox `BudgetThresholdCrossed` | - | - |
| 06 Executive dashboard | DONE | 12 SQL files in `Analytics/Dashboard`, `dashboard-page.ts` | - | - |
| 07 Access and activity (3 roles, sign-in, scope, audit) | DONE | JWT + refresh cookie, `IProjectScope`, `audit_events`, admin area (slice 09) | - | - |

## §3 Full-stack engineering

| Item | Status | Evidence or gap | Owner | Effort |
|---|---|---|---|---|
| 3.1 Responsive shell, forms, validation, confirm, loading/empty/error, refresh after mutation | DONE | shared `data-table`, `async-state`, `confirm-dialog`; refetch rule. Mobile layout only spot-checked | You (check mobile width) | S |
| 3.1 Reusable architecture explained | DONE | `docs/walkthrough/*`, slices | - | - |
| 3.1 Accessibility | PARTIAL | aria labels, icon+text badges. No automated or manual contrast/keyboard audit | Claude | S |
| 3.2 Real endpoints, route list | DONE | 17 controllers; `docs/api-routes.md`; Swagger UI at `/swagger` | - | - |
| 3.2 OpenAPI collection exported | PARTIAL | Swagger is live but no exported `openapi.json` / Postman file in repo | Claude | S |
| 3.2 Server-side rules, HTTP status codes, ProblemDetails | DONE | `GlobalExceptionHandler`, validators, tests | - | - |
| 3.2 Transaction integrity | DONE | `Decide_WhenTheOutboxInsertFails_RollsBack...`; post cost + audit in one transaction | - | - |
| 3.3 Schema, constraints, indexes, migrations | DONE | `Migrations/20261018093705_InitialSchema`, 27 tables, CHECKs, triggers; `database-design.md` | - | - |
| 3.3 Cardinality and join fan-out explained | DONE | `database-design.md` §6, one CTE per one-to-many table | - | - |
| 3.3 Non-trivial aggregation and a constraint demo | DONE | `kpis.sql`; self-approval CHECK (K4 #3) | - | - |
| 3.4 Real authentication | DONE | Identity + JWT, lockout, rate limit; `security-notes.md` | - | - |
| 3.4 Contributor denied on approve and post | DONE | `Decide_AsContributor_Returns403_AndNothingChanges`, `Post_AsContributor_Returns403AndCostStaysDraft` | - | - |
| 3.4 Cross-project access / ID escalation | DONE | `Get_ProjectOutsideScope_Returns404` | - | - |
| 3.4 Mitigations explained (SQLi, tokens, cookies, CORS) | DONE | `docs/security-notes.md` | - | - |
| 3.5 Logging and correlation id | DONE | Serilog, `CorrelationIdMiddleware`, id in ProblemDetails and audit rows | - | - |
| 3.5 Concurrency and double-submit | DONE | `Update_WithStaleVersion_*` (xmin), `CreateDraft_SameIdempotencyKeyTwice_*` | - | - |
| 3.5 At least five automated checks + manual checklist | DONE | 241 tests; `docs/manual-test-checklist.md` | - | - |
| 3.5 Git history, setup, `.env.example`, import | DONE | 14 conventional commits, README quick start, `scripts/import-data.*` | - | - |
| 3.5 Docker Compose | PARTIAL | `docker-compose.yml` has only `db`; no api/web images (README says so). Optional | Claude | M |
| Frontend README | PARTIAL | `src/frontend/pmo-web/README.md` is still the Angular CLI default | Claude | S |

## §4 Finance definitions
| Item | Status | Evidence or gap | Owner | Effort |
|---|---|---|---|---|
| Current budget = initial + approved revisions only | DONE | `RevisionForecastCategoryTests.PendingRevision_*`, `RejectedRevision_*` | - | - |
| Actual = posted costs on or before cutoff; drafts excluded | DONE | `CostTests.Spend_ExcludesDraftsAndPostCutoffCosts_AndAReversalNetsOut` | - | - |
| Remaining, utilization, zero-budget handling | DONE | `FinanceReadTests`, unit tests in `Pmo.UnitTests/Finance` | - | - |
| EAC and projected variance, documented rule | DONE | `database-design.md` §6; `Forecasts_WithoutRows_*` | - | - |
| Approved CR does not auto-create spend or revision | DONE | no code path does it; documented | - | - |
| Controls: actor/time, no silent delete of posted | DONE | immutability trigger, reversal entries, `posted_by_id/at` | - | - |
| Insight: overrun alert, project comparison, drill to transactions | DONE | `finance_by_project.sql`, `drilldown_costs.sql`, outbox alert | - | - |

## §5 Dashboard
| Item | Status | Evidence or gap | Owner | Effort |
|---|---|---|---|---|
| Delivery, risk, finance KPI cards | DONE | `kpis.sql`, `DashboardTests.Kpis_Unfiltered_MatchIndependentSqlOverTheWholeDatabase` | - | - |
| At least 5 visuals | DONE | `chart-options.ts` (health, budget vs actual vs EAC, monthly trend, category, overdue by owner, risk matrix) | - | - |
| Milestone roadmap visual | MISSING | README lists it as not built; the brief only gives it as an example, so not required | Claude | M (optional) |
| At least 4 coordinated filters, URL params | DONE | `dashboard-filters.ts` | - | - |
| 2 drill-downs (finance transactions, delivery tasks/risks) | DONE | `drilldown-page.ts`, `drilldown_costs/tasks/risks.sql` | - | - |
| Recompute after task update and new posted cost | DONE (API) / MANUAL (browser) | tests show next read changes; you should show it live once | You | S |
| Explain two KPIs (tables, filter, formula) | DONE | `docs/walkthrough/07-dashboard.md`, demo script | - | - |

## §6 Synthetic data (counts from the live DB)
| Item | Status | Evidence or gap | Owner | Effort |
|---|---|---|---|---|
| Reproducible generator, fixed seed 20260930 | DONE | `tools/DataGenerator`, `DataGeneratorTests` | - | - |
| Sizes: 3 depts / 15 users / 8 projects / 80 tasks / 24 milestones | DONE | 3 / 18 / 10 / 118 / 33 | - | - |
| 20 risks, 15 CRs + audit, 80 costs, revisions, 4 categories, forecasts | DONE | 27 / 18 (48 history) / 123 / 8 / 5 / 270 | - | - |
| Snapshots per project, automation runs | DONE | 90 snapshots, 30 runs, 9 outbox | - | - |
| Edge cases (no spend, nearing budget, forecast overrun, 5 overdue, due on cutoff, unassigned, completed project, closed high risk, returned/rejected CR, approved/unapproved revision, draft + post-cutoff costs, no-forecast project) | DONE | psql: overdue 8, due on cutoff 1, unassigned 2, completed 1, closed high 1, open high 3, returned 2, rejected 2, revisions approved 6 / pending 1 / rejected 1, drafts 19, after cutoff 2, no forecast 2, no posted spend 2; asserted in `EdgeCaseChecks` | - | - |
| Generated CSVs committed | DONE | 19 files in `data/csv` | - | - |
| Data dictionary | PARTIAL | table/column docs are inside `database-design.md` (597 lines); no short standalone data dictionary for the CSVs | Claude | S |
| Production uses today or chosen date | DONE | `Reporting:DefaultAsOf` + `asOf` parameter, documented | - | - |

## §7 SQL and Power BI
| Item | Status | Evidence or gap | Owner | Effort |
|---|---|---|---|---|
| Reporting views for Power BI | DONE (session C+D) | migration `ReportingViews`: 8 views (`dim_date`, `dim_project`, `vw_*_facts`, `vw_project_financials`); `ReportingViewTests` (5 tests) | - | - |
| 4 commented SQL queries (overdue, project finance, open high risks, period spend/forecast or milestones) | DONE (session C+D) | `sql/reports/01..04_*.sql` + README, run against the live DB | - | - |
| Power BI `.pbix` (model, date table, 5 DAX measures, slicers, page) | MANUAL | build in Power BI Desktop; inputs ready: `powerbi/README.md` (connection, model, filter direction, page design), `powerbi/measures.dax` (12 measures) | You | L |
| Reconcile 4 KPIs across SQL, web, Power BI | PARTIAL | `docs/reconciliation.md` has 7 KPIs with SQL and web values filled and discrepancy notes; Power BI column to fill after the report is built | You | S |

## §8 Automation and Access
| Item | Status | Evidence or gap | Owner | Effort |
|---|---|---|---|---|
| Automation with trigger, rules, run, log, retry, dedup | DONE | `OutboxProcessor`, `OutboxTests.Worker_ProcessingTheSameMessageTwice_*`, `automation_runs`, admin UI Retry | - | - |
| Access `.accdb` with VBA (optional) | MISSING | CLAUDE.md `access/` folder absent; needs Microsoft Access | You (Claude writes VBA and queries) | M |

## §9 AI development log
| Item | Status | Evidence or gap | Owner | Effort |
|---|---|---|---|---|
| At least 4 iterations | DONE | 6 entries in `docs/ai-development-log.md` | - | - |
| At least 1 AI-wrong case | DONE | #1 (Program clash), #5 (seed audit names), #6 (integer division) are real | - | - |
| Prompt strategy | DONE | section at top | - | - |
| Disclosure table | PARTIAL | table rows are blank; "accepted/rejected" columns need your own words | You | S |
| Entry for the admin-UI bug (`multiTemplateDataRows`) and demo-password 401 | MISSING | not yet logged | Claude (draft) / You (edit) | S |

## §10 Acceptance tests
| Test | Status | Evidence or gap | Owner | Effort |
|---|---|---|---|---|
| A Persistence | DONE (auto) / MANUAL | `ProjectWriteTests`; checklist A not yet ticked in browser | You | S |
| B Finance mutation | DONE (auto) / MANUAL | `CostTests`; checklist B | You | S |
| C Approval transaction | DONE (auto) / MANUAL | `ChangeRequestTests`; checklist C | You | S |
| D Authorization denial | DONE | two Contributor 403 tests; checklist D | You (show in Swagger) | S |
| E Dashboard integrity | DONE (auto) / MANUAL | `DashboardTests` | You | S |
| F Analytics agreement | PARTIAL | SQL and web agree (tested); Power BI column in `docs/reconciliation.md` | You | S |
| G Automation / VBA | PARTIAL | automation DONE; Access missing | You | M |
| H Engineering quality | DONE | 241 tests, error log with correlation id | - | - |

## §11 Submission checklist
| Item | Status | Evidence or gap | Owner | Effort |
|---|---|---|---|---|
| Source, tests, Git history, local setup | DONE | | - | - |
| Hosted URL | MISSING (optional) | local only; no API/web Dockerfile | Claude + You | L |
| Schema/migrations, generator, CSVs, import commands, route list | DONE | exported OpenAPI file missing (see §3.2) | - | - |
| Power BI `.pbix` + commented SQL + reconciliation notes | MISSING | see §7 | You + Claude | L |
| Access `.accdb` | MISSING | optional | You | M |
| Automation evidence | PARTIAL | code and tests exist; no saved run-evidence file (screenshot, log excerpt) | You | S |
| Architecture and security notes | PARTIAL | security notes DONE; architecture is one README paragraph plus slices, no diagram or single architecture note | Claude | S |
| 6-8 slide deck | MISSING | no slides in repo | You (Claude drafts outline and speaker notes) | M |

## Specific items you asked about

1. **No reporting views (`\dv` empty): confirmed.** No views, no `sql/reports/` folder. Needed before Power BI (see §7).
2. **Outbox message 7 root cause: not a runtime bug, it is seeded on purpose.** `AutomationSeeder.cs:74` writes the row as `Failed`, 5 attempts, `last_error` "Notification channel unavailable (simulated)", paired with `automation_runs` row `seed-run-7` (status partial). The worker only claims `pending` rows, so it never touches it; only the admin Retry (now in the UI) resets it to pending. After my manual retry the worker processed it, so the retry path works. The audit-time DB showed it `processed` because it had been retried through the UI, not because of the importer: the importer truncates every table and reloads the CSVs, so a re-import restores the failed row. Done in session B: note in slice 06 and `docs/automation-evidence.md`, `scripts/reset-db.*`, real run evidence captured.
3. **CSVs committed: yes**, 19 files in `data/csv`. Standalone data dictionary: no (see §6).
4. **API route list: yes** (`docs/api-routes.md`, 107 lines). **Exported OpenAPI/Postman file: no.**
5. **Architecture and security notes:** security DONE; architecture thin; **frontend README is still the Angular default**.
6. **AI log:** 6 real iterations and at least 3 "AI was wrong" cases, so it meets the minimum. Disclosure table empty; two newer mistakes not logged.
7. **Flows never exercised in a browser** (code and API tests exist, UI unverified): task progress update, post and reverse cost, budget revisions, forecast grid, change-request submit and approval. Only admin screens and the dashboard were browser-checked.

## Other findings
- `README.md` Status says "no UI for category admin / no admin UI": now outdated (slice 09 built it). Also `docs/manual-test-checklist.md` section G and `docs/walkthrough/00-differences.md` should mention the admin UI.
- `CLAUDE.md` repository layout lists `sql/reports/`, `powerbi/`, `access/`, which do not exist yet.
- `docker-compose.yml` runs only the database although CLAUDE.md says postgres, api, web.
- Two Postgres containers are running locally (`pmo-db` on 5433 and `pmoportal-db-1`); harmless but check you demo against the right one.
- No frontend unit or e2e tests (`*.spec.ts`); backend coverage is strong, so this is a risk only if asked.
- Test warnings: `EF1002` raw-SQL interpolation in `CostTests.cs:193,276` (test code only) and nullable warnings in `ProjectMemberTests.cs`. Low priority.

## Proposed work sessions for Claude items (highest assessment weight first)
Not started. Waiting for your approval.

1. **Session A, browser verification of the core flows (S-M).** You and Claude click through checklist A-E in the browser (project create, task progress, post/reverse cost, revisions, forecast grid, CR approval, dashboard recompute). Fix any UI bug found, tick the checklist, log any AI mistake. This has the highest weight (full-stack correctness, finance, authorization, dashboard) and the most unknown risk, so it goes first.
2. **Session B, reset-db script and automation evidence (S).** `scripts/reset-db` that drops, migrates and imports (brings back failed outbox 7), note on the seeded failure, saved run evidence for §8.1.
3. **Session C, reporting SQL and views (M).** `sql/reports/` with four commented queries, migration with reporting views (projects, tasks, risks, costs, forecasts, date dimension), design doc update, integration test that views match the dashboard KPIs.
4. **Session D, reconciliation and Power BI inputs (M).** Four-KPI reconciliation table (SQL vs web vs Power BI placeholder), DAX measure text, model and relationship notes. You then build the `.pbix` (MANUAL).
5. **Session E, docs polish (S).** Frontend README, architecture note with diagram, standalone data dictionary, exported `openapi.json` plus route list check, README status and checklist updates, AI log entries and disclosure draft.
6. **Session F, slides outline (S).** 6-8 slide outline with speaker notes for you to build in PowerPoint.
7. **Optional, only after the above:** Access VBA content (you build the `.accdb`), Dockerfiles and compose for api/web, milestone roadmap visual, accessibility audit.
