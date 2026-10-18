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
