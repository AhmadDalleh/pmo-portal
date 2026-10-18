# Slice 05 — Finance (F1–F10)

## What changed
- **Domain**: `CostTransaction.Post / EnsureDraft / Reverse`, `BudgetRevision.Decide`, `OutboxMessage.Create`.
- **Application** (`Features/Finance`): `CostService` (list, draft, edit, delete, post, reverse), `BudgetRevisionService`, `ForecastService`, `CostCategoryService`, `FinanceQueryService`, `CostMapper`, `IFinanceReadModel`, commands, DTOs, validators.
- **Infrastructure**: `Analytics/FinanceReadModel.cs` (by-category and monthly SQL, one CTE per table).
- **Api**: `CostsController`, `FinanceController`.
- **Tests**: +10 unit, +36 integration. Total 31 unit + 136 integration, all passing.

## Request flow (post a cost)
Angular cost table → confirm dialog → `FinanceApi.postCost` → `POST /api/finance/costs/{id}/post` → `CostsController` (policy `CanPostCosts`) → `CostService.PostAsync` → `IProjectScope.EnsureCanManageAsync` → KPI read before posting → **transaction**: `cost.Post()` (entity rule) + `audit_events` row + maybe `outbox_messages` row → `SaveChanges` → commit → DTO. Tables: `cost_transactions`, `audit_events`, `outbox_messages`.
Summary, dashboard and drill-down read `cost_transactions` with SQL, so they change on the next read.

## Key decisions
- **Money rules in the entity, KPIs in SQL.** `Post/Reverse/Decide` live on the entities (testable, cannot be bypassed); every figure shown is the §6 SQL (`ProjectPortfolio.sql` reused for the summary, plus two small queries for charts).
- **Reversal dated like the original.** The correction nets out in the same period and at any as-of date from then on; the reason is stored in the description and the audit row. The alternative (today's date) would leave the original month wrong.
- **Posted is immutable twice**: the service says 409 `posted_immutable`, and trigger `trg_cost_transactions_posted_immutable` blocks even raw SQL (tested).
- **Idempotency**: `Idempotency-Key` header, looked up before insert, `UNIQUE(idempotency_key)` as the safety net; a repeat returns the original (200). Reusing a key for another project is a 409.
- **Threshold alerts (H2)** are queued inside the posting transaction; `dedup_key = budget:{project}:{90|100}` is UNIQUE, so the same alert is never queued twice. The worker that turns them into notifications comes in slice 6.
- **Forecast grid**: the whole grid is one `PUT`, an upsert in one transaction; the reporting month itself is closed (only later months), consistent with FTC being "months after the as-of month". Last write wins (the table has no version column).
- **No category delete**: used categories can only be deactivated, so history stays valid.

## Rules and where they are enforced
| Rule | Where |
|---|---|
| Amount > 0; reversal < 0 and linked | validator, entity, CHECK `ck_cost_transactions_amount_sign` |
| Posted never edited/deleted | entity + trigger |
| Contributor cannot post/create/reverse | policy `CanPostCosts` (403) + project management check |
| Out-of-scope project => 404 | `IProjectScope` |
| One reversal per cost | service pre-check (409) + UNIQUE `reverses_transaction_id` |
| Revision: not own, not twice, reason required | `BudgetRevision.Decide` + CHECK `decided_by_id <> requested_by_id` |
| Only approved revisions with `effective_date <= asOf` count | `ProjectPortfolio.sql` |
| Reduction cannot make budget negative; CR link must be approved and same project | `BudgetRevisionService.RequestAsync` (400) |
| Drafts, post-cutoff costs never counted; reversals net | SQL `status='posted' AND txn_date <= asOf` |

## Transaction behaviour
Post and reverse use an explicit transaction (status/row + audit [+ outbox]); any exception means no commit and the cost stays a draft. A test installs a trigger that fails the audit insert and proves the cost stays a draft with no `posted_at`.

## How to demo (2 min)
1. As `pm3` (owns PRJ-005): `POST /api/projects/5/costs` with header `Idempotency-Key: <guid>` and body `{categoryId:1,txnDate:"2026-09-20",amount:50000,description:"Test"}` → 201. Repeat the same call → 200, same id.
2. `GET /api/projects/5/finance/summary` → spend 248,000 (draft not counted). `POST /api/finance/costs/{id}/post {version}` → spend 298,000, utilization 48.1%.
3. Post again → 409 `already_posted`. As `con2` (member): same call → 403.
4. `POST /api/finance/costs/{id}/reverse {reason}` → spend back to 248,000; the original row is unchanged. In psql `update cost_transactions set amount=1 where id=<posted>` → trigger error.
5. As `admin1`: `GET /api/projects/8/budget-revisions` shows the pending +100,000; decide it as approved → current budget 2,050,000 only from 2026-10-15 (use `?asOf=2026-10-31` to see it).

## Likely interview questions
1. **How do you prevent double posting?** State check in the entity (`already_posted`), `xmin` version on update, and the draft→posted UPDATE is the only one the trigger allows.
2. **What happens if the outbox insert fails during posting?** Same transaction: nothing commits, the cost remains a draft, the user sees an error with a correlation id.
3. **Why reversal instead of edit?** Audit trail and reproducible past reports; the trigger enforces it in the database.
4. **How is "newly crosses 90%" decided?** Spend/budget before vs after this posting (as of the reporting date); only a before<90% and after>=90% move queues a message, and the UNIQUE dedup key stops repeats.
5. **What if the same form is submitted twice?** The Idempotency-Key: the second request returns the first row, no duplicate; disabling the button in the UI is only a convenience.

## Live-change rehearsal
"Alert at 80% instead of 90%": in `CostService.QueueThresholdMessagesAsync` change the two `0.90m` literals to `0.80m` and the threshold label `90` to `80` (the dedup key and message use it). `near_budget` in `ProjectPortfolio.sql` and `ProjectHealth` also use 0.90 and are separate definitions (database-design §6). Then adapt the test `Post_CrossingNinetyAndHundredPercent...`: 85,000 would now already cross.
