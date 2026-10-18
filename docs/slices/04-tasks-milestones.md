# Slice 04 — Tasks, milestones, progress (C1–C7)

## What changed
- **Domain**: `ProjectTask.ApplyProgress` (done forces 100 and stamps `completed_at`; leaving done clears it), `Milestone.Complete`.
- **Application**: `Features/Tasks` (`TaskService`, `TaskMapper`, `TaskRules`, commands, validators), `Features/Milestones`, `Features/Snapshots` (`SnapshotService`, `ISnapshotRunner`).
- **Infrastructure**: `Analytics/SnapshotRunner.cs` (one upsert statement).
- **Api**: `TasksController`, `MilestonesController`, `SnapshotsController`.
- **Generator**: snapshot counts now exclude cancelled tasks (same definition as the runner).
- **Tests**: +13 unit (task/milestone rules, indicator boundaries), +29 integration. Total 21 unit + 100 integration, all passing.

## Request flow (C2, a contributor updates their task)
Angular task row → `TasksApi.updateProgress` → `PATCH /api/tasks/{id}/progress` → `TasksController` → `TaskService.UpdateProgressAsync` → `IProjectScope.EnsureCanViewAsync` (404) → manager or assignee? (403) → `task.ApplyProgress` → one `SaveChanges` (task + audit row) → `tasks`, `audit_events`.
Dashboard/project progress changes on the next read because it is computed by SQL (`ProjectPortfolio.sql`), never stored on the client.

## Key decisions
- **Rule on the entity.** `ApplyProgress` is the only code that changes status and progress together, so the "done = 100" rule cannot be bypassed and is unit tested without a database. The DB CHECK `ck_tasks_done_is_100` is the second guard.
- **Contributors may not cancel or reopen** (my addition, flagged): cancelling removes a task from project progress, which is a management decision.
- **Indicators computed on the server** for the requested `asOf`, so the UI only draws icon + text (C4); the SQL predicate `Overdue(asOf)` is one expression shared by the list filter and later by the dashboard drill-down.
- **Snapshots are an upsert in SQL** (`ON CONFLICT (project_id, snapshot_date) DO UPDATE`), so rerunning is idempotent. A daily background trigger is added with the other background job in slice 6.
- **Sorting** uses an allow-list switch, never a column name from the request.

## Rules and where they are enforced
| Rule | Where |
|---|---|
| Assignee must be a member; milestone same project | service (400) + composite FK in DB |
| done => progress 100, completed_at | `ApplyProgress` + CHECK |
| Contributor: own tasks only, not cancel | `TaskService.UpdateProgressAsync` (403) |
| Out of scope task => 404 | `EnsureCanViewAsync` before anything else |
| Stale edit => 409 | `xmin` as `Version` |
| Overdue = due < asOf, open; due today is not overdue | `TaskRules` (+ unit tests on the boundary) |
| Snapshot run is Administrator only | `[Authorize(Policy = AdminOnly)]` |

## Transaction behaviour
Create task/milestone: explicit transaction (first save gives the id, second saves the audit row, commit). Updates: one `SaveChanges` carries entity + audit row. A failure anywhere leaves no audit row without its change.

## How to demo (2 min)
1. Log in as `pm5` (owns PRJ-008). `GET /api/projects/8/tasks?overdue=true` → 1 overdue task.
2. Log in as the task's assignee (a contributor of PRJ-008, e.g. `con1`): `GET /api/me/tasks`, then `PATCH /api/tasks/{id}/progress` with `{ "progressPct": 100, "status": "done", "version": <v> }`.
3. `GET /api/projects/8/summary` before and after: progress and overdue count change, no client math.
4. Same PATCH as `con2` (not assignee) → 403. Repeat with the old version → 409.
5. Admin: `POST /api/admin/snapshots/run?date=2026-10-31` twice; `select count(*) from progress_snapshots where snapshot_date='2026-10-31'` stays 9.

## Likely interview questions
1. **Where is "done means 100%" enforced?** Entity method `ApplyProgress`, mirrored by a CHECK constraint.
2. **Why is a task due today not overdue?** Definition in database-design §6; `due_date < asOf`. Unit tests cover the boundary.
3. **How do you stop a contributor editing someone else's task by id?** Scope check (404 outside the project), then assignee check (403).
4. **How do two people editing the same task not overwrite each other?** `xmin` version; stale update affects 0 rows, EF raises a concurrency exception, API returns 409.
5. **Is progress stored?** Only in the history table. Live progress is `SUM(weight*progress)/SUM(weight)` computed per query.

## Live-change rehearsal
"Due-soon window of 7 days instead of 14": change `DueSoonDays = 14` in `TaskRules` (and the `[InlineData("blocked", "2026-10-14", "due_soon")]` unit row to `2026-10-07`).
