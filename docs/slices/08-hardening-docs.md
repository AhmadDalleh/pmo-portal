# Slice 08 — Test hardening, README and documents (K4–K8)

## What changed
- `README.md` (setup, demo logins, architecture, honest status), `docs/manual-test-checklist.md` (acceptance tests A–H and the K4 coverage map), `docs/security-notes.md`.
- No new production code: the 13 required K4 checks were already written inside slices 3–7 as each rule was built. This slice only audited them: every one maps to a named automated test (table in the checklist).
- Final run: 51 unit + 190 integration tests passing; `ng build` (production) passes, initial bundle about 1.1 MB.

## Decisions
- Tests were written with each slice rather than at the end, so hardening became an audit. The seeded database is the fixture; mutating tests create their own project, so seeded figures stay exact for the read-only tests.
- Each forced-failure test (project create, cost post, change request decision) installs a temporary database trigger to prove nothing is half-saved.

## Not done (honest list)
Power BI (J), slides (K9), Access (L), Docker images for api/web, admin UI screens, milestone roadmap visual, `docs/data-dictionary.md` (database-design §3 is the dictionary), frontend unit tests, accessibility audit with a tool (labels, icons with text and keyboard reachability were built in but not machine-checked).

## Live-change rehearsal
"Lock accounts for 10 minutes": `options.Lockout.DefaultLockoutTimeSpan` in `Infrastructure/DependencyInjection.cs` (`AddAuth`).
