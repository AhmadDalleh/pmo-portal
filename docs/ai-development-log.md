# AI Development Log

Required by assignment §9: ≥ 4 meaningful iterations, ≥ 1 case where AI was wrong / insecure / inefficient / analytically incorrect, prompt strategy, and disclosure.

## Prompt strategy
- **Specs first:** Claude Code works from `docs/user-stories.md` and `docs/database-design.md`; prompts reference story IDs instead of re-describing features.
- **Plan → approve → implement → explain-back** per vertical slice (`.claude/skills/pmo-feature-slice`).
- **Business rules and formulas are written by me in the design doc**; AI implements them, I verify with tests against seeded edge cases.
- **Debugging:** give the failing test, the log line with correlation id and the SQL; ask for a hypothesis before a fix.

## Disclosure
| Area | AI-assisted? | What I personally validated / changed |
|---|---|---|
| Schema & constraints | | |
| Finance calculations | | |
| Auth & RBAC | | |
| Dashboard SQL | | |
| Angular UI | | |
| Data generator | | |
| Tests | | |

---

## Iteration template (copy per entry)

### #N — <short title> (story ids, date)
- **Requirement / problem:**
- **Prompt (summary or excerpt):**
- **AI result:**
- **Accepted:**
- **Rejected / changed, and why:**
- **Revised implementation (files / commit):**
- **Test evidence:** (test names, output, SQL check)
- **Was the AI wrong?** (yes/no — if yes: how I found it and how I corrected it)

---

### #1 — Generator entry point clashed with the API test host (I1–I3, 2026-10-18)
- **Requirement / problem:** integration tests need to call the generator and importer in-process (Testcontainers), so the test project references `tools/DataGenerator`.
- **Prompt (summary):** "Build slice 2: data generator + import; assert the §10 edge cases; seed 20260930; password from DEMO_USER_PASSWORD."
- **AI result:** the generator used top-level statements in `Program.cs`. The first `dotnet test` failed with `CS0433: type 'Program' exists in both DataGenerator and Pmo.Api` because `PmoApiFactory : WebApplicationFactory<Program>` could no longer tell them apart.
- **Accepted:** the layout (seeders, plan tables, `EdgeCaseChecks`, COPY importer).
- **Rejected / changed, and why:** replaced top-level statements with a named `GeneratorCli.Main`, so no second `Program` type exists. Cheaper than aliasing in every test file.
- **Revised implementation:** `tools/DataGenerator/GeneratorCli.cs`.
- **Test evidence:** `dotnet test` → 24 passed (8 earlier + 16 new).
- **Was the AI wrong?** Yes (a design slip, caught by the compiler): a console tool that other projects reference must not use top-level statements.

---

### #2 — Claims mapping, EF projection and fallback policy (A1–A3, B1–B6, 2026-10-18)
- **Requirement / problem:** JWT sign-in with `sub`/`role` claims, scoped project API, `[AllowAnonymous]` only where needed.
- **Prompt (summary):** "Slice 3: auth (Identity + JWT + refresh cookie), project scope, projects, members, lookups."
- **AI result:** first test run: 45 of 79 tests failed with 500s.
- **Accepted:** overall layering (`IPmoDbContext`, `IProjectScope`, read model with embedded SQL).
- **Rejected / changed, and why:**
  1. The JWT handler silently renames claims (`sub` becomes a long URI), so `CurrentUser` found no `sub` ("Request is not authenticated"). Fix: `MapInboundClaims = false` in `AddAuth`.
  2. `UserDirectory` filtered a `select new UserSummary(...)` projection; EF Core cannot translate `Where` over a constructor projection. Fix: join, filter on the joined columns, project last.
  3. The "must be signed in" fallback policy also protected `/api/health` and made unknown routes return 401 instead of 404. Fix: `[AllowAnonymous]` on health; the unknown-route test now signs in first.
  4. Integration tests that create projects broke count assertions of other tests. Fix: read-only tests filter on `PRJ-` codes.
- **Revised implementation:** `Infrastructure/DependencyInjection.cs`, `Identity/UserDirectory.cs`, `HealthController.cs`, project tests.
- **Test evidence:** `dotnet test` → 8 unit + 71 integration passing.
- **Was the AI wrong?** Yes, three times (framework defaults I assumed). Found by reading the server log with the correlation id; each fix has a test that would fail without it.

---

### #3 — Smallint cast and over-strict test assumptions (C1–C7, 2026-10-18)
- **Problem:** four of 100 new tests failed on first run.
- **AI result / what was wrong:** (1) tests read `smallint` columns as `int` (PostgreSQL returns Int16); (2) I assumed the on-hold project has exactly one overdue milestone, but the seed rule leaves every past-due milestone of an on-hold project pending; (3) the history test assumed the newest snapshot is the seed one, but the "run snapshot" test adds a later one.
- **Fix:** read as `short`; assert the specific milestone is among the overdue ones; look up the 2026-09-30 snapshot explicitly. No production code changed, the assumptions in the tests were wrong.
- **Was the AI wrong?** Yes, in the tests (not the code). Found from the failure message in one run.

---

### #4 — Reversal date and alert rules (F6, F7, H2, 2026-10-18)
- **Requirement:** reversal row for a posted cost; alert when a posting newly crosses 90% / 100%.
- **Prompt (summary):** "Slice 5: revisions, draft/post/reverse costs, forecasts, summary."
- **AI result / decision:** the brief gives no date for the reversal. A reversal dated today would not net out at the 2026-09-30 reporting date, so the demo "reverse, spend drops" would silently fail. Chose "same date as the original" and documented it (`CostTransaction.Reverse`). The threshold rule compares utilisation before and after the posting at the reporting date and relies on the UNIQUE dedup key to stay once-only.
- **Accepted:** all code passed its 46 new tests on the first run.
- **Was the AI wrong?** No wrong code this slice; the point worth defending in the interview is the reversal-date trade-off above.

---

### #5 — Seed data used different audit action names than the API (E3, A5, 2026-10-18)
- **Problem:** the audit-log test filtered `action=change_request.decided` and found nothing.
- **AI result / what was wrong:** the data generator (slice 2) wrote actions like `change_request.approved`, while the service (slice 6) writes `change_request.decided`, so the audit screen would have shown two vocabularies for the same thing. Same for `budget_revision.*`.
- **Fix:** one mapping in `GovernanceSeeder.ActionFor` and `SeedGenerator.AddRevisionAudit` using the API names; CSVs regenerated.
- **Test evidence:** `AdminTests.AuditLog_FiltersByActionAndProject_NewestFirst`.
- **Was the AI wrong?** Yes: an inconsistency between two slices written at different times; found by a test that reads seed data through the real endpoint. Lesson for the log: seed data and services must share constants, not copies of strings.

---

### #6 — Integer division in the portfolio progress KPI (C6, G1, 2026-10-18)
- **Problem:** the dashboard KPI "portfolio progress" returned `63` where the independent query gave `63.2416...`.
- **AI result / what was wrong:** `SUM(weight * progress_pct)` is an integer in PostgreSQL, and I divided two integers in `kpis.sql`. The earlier project query had the `::numeric` cast; this new copy did not. A rounding-looking difference, but it would have made the dashboard disagree with the project pages and Power BI.
- **Fix:** `tk.weighted_progress::numeric / NULLIF(tk.total_weight, 0)`.
- **Test evidence:** `DashboardTests.Kpis_Unfiltered_MatchIndependentSqlOverTheWholeDatabase` compares every KPI with a raw SQL statement written separately.
- **Was the AI wrong?** Yes. Found because the test computes the expected value with its own SQL, not with the code under test.

---

### #7 — Expandable audit row passed `ng build` but broke every table at runtime (A4, A5, H3, F2, 2026-10-18) — DRAFT, edit in your words
- **Requirement / problem:** the audit screen needs an expandable row showing the event JSON, using the shared `data-table`.
- **Prompt (summary):** "Build the Admin UI with the shared components; audit rows expand to show correlation id and data JSON."
- **AI result:** extended `DataTable` with an optional `detail` template and added a second `mat-row` definition for the detail row. `ng build` and the tests passed. In the browser the Users tab stayed on "Loading..." forever.
- **What was wrong:** Angular Material allows only one default row definition unless the table has `multiTemplateDataRows`; the check happens at runtime, so the compiler never saw it. The same error would have broken every table in the app (projects, tasks, costs), not only the admin ones.
- **How it was found:** the browser console showed `There can only be one default row without a when predicate function. Or set multiTemplateDataRows`; the table component threw while rendering, so the page never left its loading state.
- **Fix:** `multiTemplateDataRows` on the `<table mat-table>` in `shared/data-table/data-table.ts`; detail rows stay hidden (`display:none`) unless that row is expanded.
- **Test evidence:** re-checked in the browser: users, audit (row expands with correlation id and JSON), automation, categories all render; `ng build` passes. There is no frontend test that would have caught it (known gap).
- **Was the AI wrong?** Yes: a green build is not proof that a UI change works. Lesson: open the page after changing a shared component, and read the console.
- **Accepted / rejected by me:** *(you fill in)*

---

### #8 — Audit report claimed the importer does not overwrite data (submission audit, 2026-10-18) — DRAFT, edit in your words
- **Requirement / problem:** explain why outbox message 7 (a seeded failed message) showed `processed` in the database.
- **Prompt (summary):** "Audit the repo against the brief; find the root cause for outbox message 7."
- **AI result:** the first version of `docs/submission-status.md` said the importer does not overwrite existing rows, so the failed example "is gone unless you reset the database".
- **What was wrong:** an assumption made without opening the code. `CsvImporter.ResetAsync` runs `TRUNCATE ... RESTART IDENTITY CASCADE` and reloads every CSV in one transaction, so a re-import always restores message 7 as `failed`. The message had been retried through the Admin screen.
- **How it was found:** while writing the `reset-db` script the importer had to be read; the truncate was visible, and a fresh reset (message 7 `failed`, 5 attempts) plus a real retry through the API confirmed it (`docs/automation-evidence.md`).
- **Fix:** corrected the status document; `scripts/reset-db.*` wraps the importer and prints the outbox afterwards.
- **Test evidence:** reset-db output showing `7 | budget:9:100 | failed | 5`, then the retry and worker run recorded in `docs/automation-evidence.md`.
- **Was the AI wrong?** Yes: an analytical claim about behaviour that was not checked in the code. Lesson: for "does X do Y" questions, read X or run it before writing the answer.
- **Accepted / rejected by me:** *(you fill in)*
