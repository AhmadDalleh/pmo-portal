# 04 — Finance: budget, costs, revisions, forecast (F1–F10)

## 1. Business goal
The PMO must know, per project, what was approved, what has really been spent, and where the project will end (EAC).
Money moves through controlled steps: draft → post → (reversal only), and budget changes need an approved revision.
Every figure comes from SQL on posted data as of a reporting date, so management and Power BI can reproduce it.

## 2. UI screens and actions (project → **Finance** tab)
| Area | Click | Result |
|---|---|---|
| Budget overview cards | Hover the (i) icon | Formula text. Cards: initial budget, approved revisions, current approved budget, actual spend, remaining, budget used (with "Near budget"/"Overspent"), forecast to complete ("No forecast entered"), EAC, projected variance ("Projected overrun"). |
| Charts | – | Spend by category (actual vs forecast), monthly spend + forecast. |
| **Costs** tab | Filter All / Drafts / Posted | Table with status badges. |
| Costs | **New draft cost** (PM/Admin of the project) | Dialog (category, date, amount > 0, description, vendor, reference). A new `Idempotency-Key` is generated when the dialog opens. |
| Costs, draft row | Upload icon = **Post** (confirm: "Post AED … Once posted it counts toward actual spend and cannot be edited…"); pencil = edit; bin = delete (confirm) | Posted rows only offer **Reverse** (needs a reason). |
| **Budget revisions** tab | **Request revision** (amount ± , effective date, reason, optional approved CR id) | Row Pending. Administrators see Approve / Reject (reason required). |
| **Forecast** tab | Edit cells, **Add month**, **Save forecast** | One request for the whole grid. First editable month = the month after the reporting month (2026-10). |
| Notifications | – | Budget alerts arrive as notifications (module 05). |

## 3. Per action
| Action | Route | Chain | Rules | Tables / transaction |
|---|---|---|---|---|
| Summary | `GET /api/projects/{id}/finance/summary?asOf` | `FinanceController` → `FinanceQueryService` → `IProjectReadModel.GetKpisAsync` (`ProjectPortfolio.sql`) | view scope; §6 formulas | reads `project_budgets, budget_revisions, cost_transactions, financial_forecasts` |
| By category / monthly | `GET …/finance/by-category`, `…/monthly` | `FinanceReadModel` (two CTEs, no fan-out) | posted, `txn_date <= asOf`; forecast months after the as-of month | reads |
| Cost list | `GET /api/projects/{id}/costs?status&categoryId&from&to&page` | `CostService.ListAsync` | view scope | reads |
| Create draft | `POST /api/projects/{id}/costs` + header `Idempotency-Key` (policy `CanPostCosts`) | `CostService.CreateDraftAsync` | manage scope; key required (400); same key → original returned with **200**; key used on another project → 409; amount > 0; active category | **tx, two saves**: `cost_transactions` then `audit_events` `cost.draft_created` |
| Edit / delete draft | `PUT` / `DELETE /api/finance/costs/{id}` | `UpdateDraftAsync` / `DeleteDraftAsync` → `CostTransaction.EnsureDraft` | posted ⇒ 409 `posted_immutable` (and DB trigger); `version` (409) | one `SaveChanges` with audit `cost.draft_updated` / `cost.draft_deleted` |
| **Post** | `POST /api/finance/costs/{id}/post` `{version}` | `CostService.PostAsync` → `CostTransaction.Post` | manage scope; already posted ⇒ 409 `already_posted`; stale version ⇒ 409; threshold check | **One transaction**: `cost_transactions` (status, posted_by_id, posted_at) + `audit_events` `cost.posted` + (maybe) `outbox_messages` `BudgetThresholdCrossed`; any failure ⇒ nothing saved, cost stays draft |
| **Reverse** | `POST /api/finance/costs/{id}/reverse` `{reason}` | `CostService.ReverseAsync` → `CostTransaction.Reverse` | only posted, not already reversed (`already_reversed`), not a reversal (`cannot_reverse_reversal`), reason required; new row is **posted, negative, same date, linked** | **tx, two saves**: new `cost_transactions` row, then `audit_events` `cost.reversed`; original untouched (trigger forbids changing it) |
| Request revision | `POST /api/projects/{id}/budget-revisions` (policy `ManageProjects`) | `BudgetRevisionService.RequestAsync` | manage scope; amount ≠ 0; linked CR must be **approved and same project**; reduction cannot take initial+approved+amount below 0 | **tx, two saves**: `budget_revisions`, `audit_events` `budget_revision.requested` |
| Decide revision | `POST /api/finance/budget-revisions/{id}/decision` `{decision:"approved"|"rejected",reason,version}` (**AdminOnly**) | `BudgetRevisionService.DecideAsync` → `BudgetRevision.Decide` | never your own (403 `self_approval_forbidden`, plus CHECK), only pending (409 `invalid_transition`), reason required | one `SaveChanges`: `budget_revisions` + `audit_events` `budget_revision.decided` |
| Forecast | `GET/PUT /api/projects/{id}/forecasts` | `ForecastService` | PUT: manage scope; months must be first-of-month **and after the reporting month** (`forecast_month_not_future`); amount ≥ 0; no duplicate cell; active categories | one `SaveChanges`: upsert `financial_forecasts` + `audit_events` `forecast.saved` |
| Categories | `GET/POST/PUT /api/finance/cost-categories` (writes ADM; UI none) | `CostCategoryService` | unique code and name; no delete | `cost_categories`, audit |

Threshold rule (`CostService.QueueThresholdMessagesAsync`): with spend `before` and `after` this posting at the reporting date, queue `budget:{project}:90` when before/budget < 0.90 ≤ after/budget, and `budget:{project}:100` when before ≤ budget < after. The dedup key is UNIQUE, so each message exists once per project ever.

## 4. Test scenarios (fresh seed; reporting date 2026-09-30)
| Type | User | Steps | Expected |
|---|---|---|---|
| Happy (post) | `pm3` | PRJ-005 → Finance → Costs → Drafts: post draft **#66** (AED 17,551, 2026-09-18) | Confirm dialog → posted. Actual spend **AED 248,000 → 265,551**, remaining 354,449, budget used **42.8%** (current budget 620,000). Charts and dashboard change on the next read. |
| Happy (draft only) | `pm3` | New draft AED 50,000 dated 2026-09-20 | Appears as Draft; spend **unchanged** (248,000). |
| Idempotency | `pm3` | Swagger `POST /api/projects/5/costs` twice with the same `Idempotency-Key` | 1st 201, 2nd **200** with the same id; one row. Missing header → 400 `idempotency_key_required`. |
| Validation | `pm3` | Draft with amount 0 / no description | 400 `errors.amount`, `errors.description`. |
| Forbidden role | `con2` (member of PRJ-005) | Swagger `POST /api/finance/costs/66/post {version}` | **403**; `select status from cost_transactions where id=66` still `draft`. |
| Out of scope | `pm1` | Post a PRJ-005 cost | 404. |
| Posted immutable | `pm3` | Try `PUT /api/finance/costs/{posted id}` / psql `update cost_transactions set amount=1 where id=<posted>` | 409 `posted_immutable`; psql: error "Posted cost transaction … is immutable". |
| Reversal | `pm3` | Reverse the posted #66 with a reason | New posted row amount −17,551 linked to 66; spend back to 248,000; second reverse → 409 `already_reversed`. |
| Seed: near budget | `admin1` | PRJ-001 summary | Spend 1,196,000 of 1,300,000 = **92.0%**, "Near budget"; EAC 1,286,000; variance +14,000. |
| Seed: forecast overrun | `admin1` | PRJ-002 summary | Budget 2,700,000, spend 1,647,000, FTC 1,300,000, **EAC 2,947,000**, variance **−247,000**, "Projected overrun". Seed reversal #38 reverses #37 (net 0). |
| Seed: no posted spend | `admin1` | PRJ-003 | Spend 0, budget used 0.0% (four drafts only). |
| Seed: no forecast | `admin1` | PRJ-005 | "No forecast entered", FTC 0, **EAC = actual spend** (we never invent a forecast). |
| Seed: pending revision | `admin1` | PRJ-008 | Pending +100,000 (effective 2026-10-15) **ignored**: current budget 1,950,000. Approve it as `admin1`: still 1,950,000 at 2026-09-30; with `?asOf=2026-10-31` → 2,050,000. |
| Seed: future-dated + rejected | `admin1` | PRJ-007 | Rejected −50,000 and approved +100,000 effective 2026-11-01 are both ignored: current budget 1,500,000. |
| Seed: post-cutoff | `admin1` | PRJ-009 | Posted cost #122 (30,000 dated 2026-10-05) **not** in spend: 599,500, **109%**, "Overspent". With `asOf=2026-10-31` spend is 629,500. |
| Revision rules | `pm5` / `admin1` | `pm5` requests −3,000,000 on PRJ-008 → 400 `budget_would_be_negative`; links CR-0014 (other project / not approved) → 400; `admin1` requests then decides own → 403 `self_approval_forbidden`; `pm5` tries to decide → 403 (policy) | as written |
| Forecast rules | `pm1` | PRJ-009 forecast: add 2026-09 or a non-first-of-month date | 400 `forecast_month_not_future` / field error; nothing saved. |
| Alert | `pm1` | Post enough in a fresh project to cross 90% (see test `Post_CrossingNinetyAndHundredPercent…`) | `outbox_messages` gets `budget:{id}:90` pending; later `…:100`; never repeated. |

## 5. SQL to verify
```sql
-- actual spend as of the reporting date (drafts and later dates excluded; reversals net)
select project_id, sum(amount) from cost_transactions where status='posted' and txn_date <= '2026-09-30' group by 1 order by 1;
-- current budget
select pb.project_id, pb.initial_amount + coalesce(sum(br.amount) filter (where br.status='approved' and br.effective_date <= '2026-09-30'),0) as current_budget
from project_budgets pb left join budget_revisions br on br.project_id = pb.project_id group by 1, pb.initial_amount order by 1;
-- forecast to complete (months after September 2026)
select project_id, sum(amount) from financial_forecasts where period_month > date_trunc('month', date '2026-09-30')::date group by 1 order by 1;
-- what the posting wrote
select id, status, posted_by_id, posted_at from cost_transactions where id = 66;
select action, entity_id, data from audit_events where entity_type = 'CostTransaction' order by id desc limit 5;
select type, dedup_key, status from outbox_messages order by id desc limit 5;
-- reversal pairs
select r.id as reversal, r.amount, o.id as original, o.amount from cost_transactions r join cost_transactions o on o.id = r.reverses_transaction_id;
-- idempotency
select count(*) from cost_transactions where idempotency_key = '<your key>';
```

## 6. Interview questions
1. **What commits when a cost is posted?** One transaction: status change, audit row and, if a threshold was crossed, the outbox message; if anything fails nothing is saved and the cost stays a draft.
2. **Why a reversal instead of editing a posted cost?** Audit trail and reproducible reports; a trigger enforces immutability even for raw SQL. The reversal carries the original's date so both net out in every period.
3. **How do you prevent double submission?** The form's `Idempotency-Key` plus a UNIQUE index returns the original row (200); posting a second time hits `already_posted`/version checks; the disabled button is only UX.
4. **How are EAC and variance defined and what if there is no forecast?** EAC = actual + forecast months after the as-of month; variance = current budget − EAC; no forecast rows means FTC 0 and a "No forecast" flag.
5. **Why is a pending or future-dated revision ignored?** Only approved revisions with `effective_date <= asOf` count (§6), so approving a revision cannot silently change past reports.
