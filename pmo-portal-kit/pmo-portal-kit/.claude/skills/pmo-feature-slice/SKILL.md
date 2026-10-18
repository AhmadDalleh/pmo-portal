---
name: pmo-feature-slice
description: Step-by-step workflow for building or changing any feature of the PMO Portfolio & Financial Control Portal end to end — migration, domain rule, application service, API endpoint, authorization, tests, Angular UI and the explain-back summary for Ahmad. Use this skill whenever the task touches a user story (A1…L1), an entity or table, an endpoint, a KPI or dashboard widget, a finance calculation, the approval workflow, the outbox automation, seed data, or an Angular screen in this repository — even if the request is phrased casually ("add the risk page", "fix the spend number", "make posting work").
---

# PMO feature slice

A *slice* is one user story (or a few tightly related ones) delivered through every layer and verified against the real database. The interviewer will ask Ahmad to open any file, trace a request end to end and change something live — so every slice must be small, conventional and explained.

Read `CLAUDE.md` first if it isn't already in context. Requirements come from `docs/user-stories.md`; tables, constraints and formulas from `docs/database-design.md`. Code patterns for each step are in `references/backend-patterns.md` and `references/frontend-patterns.md` — open the relevant one when you reach that step instead of inventing a new pattern.

## Step 0 — Locate the slice
1. Identify the story IDs and quote their acceptance criteria.
2. Check the permission matrix row(s) in `user-stories.md`.
3. Check which tables/KPIs from `database-design.md` are involved.
4. If anything is ambiguous or contradicts the assignment PDF, ask one precise question before continuing.

## Step 1 — Plan (show it, then wait for "go")
Output a short plan:
- Stories + acceptance criteria covered
- DB change (new table/column/constraint/index/trigger/view) — or "none"
- Domain rule(s) and where they live
- Endpoints: method, route, request/response DTO, success + error status codes
- Authorization: role policy + resource scope rule + expected 403/404 cases
- Transaction boundary (which rows commit together)
- Tests to add (names)
- Frontend pieces (if any)
- Out of scope for this slice

Keep the plan to what fits in one or two commits. If it's bigger, split it and say so.

## Step 2 — Database
- Add/modify the EF entity configuration with constraints exactly as in the design doc (CHECK names prefixed `ck_`, indexes `ix_`, unique `ux_`).
- Raw SQL objects (triggers, views, partial indexes, generated columns if not expressible) go in the migration via `migrationBuilder.Sql`, with a `Down` that removes them.
- Generate the migration, read the generated code, and point out anything surprising (e.g. dropped columns).
- Update `docs/database-design.md` if the schema changed.

## Step 3 — Domain
- Put invariants and state transitions on the entity (e.g. `changeRequest.Decide(...)`, `cost.Post(...)`), throwing `DomainRuleException` with a stable `code` (`invalid_transition`, `self_approval_forbidden`, `posted_immutable`, ...).
- No EF, HTTP, or `DateTime.Now` in the domain — pass `actorId` and `now` in.
- Write unit tests for every rule branch before moving on.

## Step 4 — Application service
- Request DTO → FluentValidation validator → service method.
- First line of every project-owned operation: load project scope; out of scope → `NotFoundException`; not permitted → `ForbiddenException`.
- Multi-row business actions: explicit transaction; entity change + history + `audit_events` + `outbox_messages` in one `SaveChangesAsync`, then commit.
- Updates carry `version`; set it as the original concurrency value.
- Return a response DTO (hand-mapped) including the new `version`.
- Read models for KPIs/dashboards: parameterized Dapper SQL following `database-design.md` §6, one CTE per one-to-many table. Drill-down queries reuse the **same** WHERE builder as the KPI.

## Step 5 — API
- Thin controller action; correct verb and status code; `[Authorize(Policy=...)]`; `[ProducesResponseType]` for success and each ProblemDetails case so Swagger documents it.
- Add the route to `docs/api-routes.md`.

## Step 6 — Integration tests
Minimum per slice that has an endpoint:
- happy path persists to DB (assert by re-querying the DB, not only the response)
- validation failure → 400 with field errors
- forbidden role → 403; out-of-scope → 404 (for sensitive routes)
- business-rule violation → 409 with the expected `code`
- for transactions: on forced failure nothing is persisted (no orphan history/audit)
- for KPIs: value equals an independent raw SQL computation on the seeded fixture

Run `dotnet test` and report the result honestly. Never weaken a constraint or assertion to make a test pass — explain the failure instead.

## Step 7 — Frontend (if the slice has UI)
- API client method in `core/api/`, feature store with signals, page/component in `features/<feature>/`.
- Reuse shared components (data-table, confirm-dialog, kpi-card, chart-card, status-badge).
- Loading / empty / error (with correlation id + retry) / success-toast states.
- After a mutation, refetch affected data from the API (including dashboard KPIs when relevant). Never compute KPIs in TypeScript.
- Map `ProblemDetails.errors` onto form fields; show `detail` for 403/409.
- Keyboard-accessible, labelled inputs; status via text + icon.

## Step 8 — Verify manually
Give Ahmad exact steps to verify: which demo user to log in as, what to click or which `curl`/Swagger call to make, what SQL to run to confirm the DB changed, and the expected numbers where relevant (asOf 2026-09-30).

## Step 9 — Explain-back (always, at the end of the slice)
Use this template — it is Ahmad's interview preparation:

```
### Slice: <story ids> — <title>
**What changed:** files grouped by layer (1 line each)
**Request flow:** Angular component → api service → HTTP <METHOD route> → controller → service → domain rule → EF/SQL → DB (name the tables)
**Key decisions & trade-offs:** 2–4 bullets (why this design, what alternative was rejected)
**Rules enforced on the server:** list, with where each lives (validator / domain / DB constraint / policy)
**Transaction & failure behaviour:** what commits together; what happens if step N fails
**How to demo (2 min):** numbered steps
**Likely interview questions:** 3–5 questions with short answers
**Live-change rehearsal:** one small change the interviewer might ask for and exactly which file/line to edit
**AI log:** draft entry if this slice had a meaningful AI iteration or correction (else "none")
```

## Step 10 — Commit
Suggest a conventional commit message (or split into several). Don't commit unless Ahmad asks.

## Guardrails specific to this project
- Finance formulas: only those in `database-design.md` §6. Drafts and txn_date > asOf never count; pending/rejected revisions never count; approved CRs never change budget or spend automatically.
- Posted costs are never updated or deleted — reversal only.
- CR decision: requester can never decide; exactly one history row per transition.
- Self-approval, posting and decision endpoints must have a Contributor-denied integration test.
- Dashboard: no client-side KPI math; drill-down sum must equal the KPI.
- Seeds: if a change affects data, keep the §10 edge cases true and the generator deterministic.
