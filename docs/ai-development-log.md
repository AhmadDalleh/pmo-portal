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
