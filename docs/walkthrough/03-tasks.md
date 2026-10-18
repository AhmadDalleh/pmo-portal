# 03 — Tasks, milestones, progress (C1–C7)

## 1. Business goal
Delivery status must come from the work itself: each task has a weight and progress, and project progress is calculated from them.
Contributors update only their own tasks; managers plan the work (tasks, milestones) and see what is late.
Overdue, due-today and due-soon indicators let the PMO act before a milestone slips.

## 2. UI screens and actions
| Screen | Click | Result |
|---|---|---|
| Project → **Tasks** | Filter *Status*, *Show: Overdue only*, sort headers, paging | Server-side list; "Timing" column shows Overdue / Due today / Due soon as icon + text. |
| Tasks | **New task** (managers) / pencil icon | Dialog: title, assignee (members), milestone, status, priority, dates, weight 1–10, progress. Choosing *Done* forces 100% on the server. |
| Tasks | Trend-up icon (managers, or the task's assignee) | "Update progress" dialog: status + progress. Contributors cannot pick *Cancelled*. |
| **My work** (menu) | Filter *Open tasks / Done / Everything*; trend icon; open-project icon | Your assigned tasks across projects, soonest due first. |
| Project → **Milestones** | **New milestone**, pencil, check icon (Complete, with confirmation) | Table: due date, status (Overdue badge when pending and past due), "tasks done / total". |
| Project → **Overview** | Progress-history chart | Monthly snapshots (progress % and spend). |
| Admin snapshot | Swagger `POST /api/admin/snapshots/run?date=2026-10-31` | Upserts one snapshot per live project (idempotent). |

## 3. Per action
| Action | Route | Chain | Rules | Tables / transaction |
|---|---|---|---|---|
| List tasks | `GET /api/projects/{id}/tasks?status&assigneeId&priority&overdue&sort&dir&page&pageSize&asOf` | `TasksController.List` → `TaskService.ListAsync` | view scope; sort allow-list (`due,title,status,priority,progress,weight`); `TaskRules.Overdue(asOf)`; indicator via `TaskRules.Indicator` | reads `tasks`, `projects`, `milestones`, users |
| My tasks | `GET /api/me/tasks?status&page&pageSize` | `TaskService.MyTasksAsync` | only my tasks in projects I can still see; default = todo/in_progress/blocked | reads |
| Create task | `POST /api/projects/{id}/tasks` (policy `ManageProjects`) | `TaskService.CreateAsync` → `ProjectTask.ApplyProgress` | manage scope; assignee must be a member (`assignee_not_member`); milestone same project (`milestone_not_in_project`, plus composite FK); validators weight/progress/dates | **Explicit transaction, two saves**: `tasks` (to get the id) then `audit_events` `task.created`; commit |
| Edit task | `PUT /api/tasks/{taskId}` | `TaskService.UpdateAsync` | same checks + `Version` (409) | one `SaveChanges`: `tasks` + `audit_events` `task.updated` |
| Update progress | `PATCH /api/tasks/{taskId}/progress` `{progressPct,status,version}` | `TaskService.UpdateProgressAsync` → `ApplyProgress` | view scope (404); not manager → must be the assignee (403 "You can only update tasks assigned to you."); non-manager cannot cancel/reopen (403); `done` ⇒ progress 100 and `completed_at`; leaving done clears it; `Version` | one `SaveChanges`: `tasks` + `audit_events` `task.progress_updated` |
| Milestones | `GET/POST /api/projects/{id}/milestones`, `PUT /api/milestones/{id}`, `POST …/{id}/complete {version}` | `MilestoneService` → `Milestone.Complete` | manage scope; complete twice → 409 `already_completed`; `completed_at` = system date | create: tx two saves (`milestones`, audit); update/complete: one save with audit |
| History / snapshot | `GET /api/projects/{id}/progress-history`, `POST /api/admin/snapshots/run` (ADM) | `SnapshotService` → `SnapshotRunner` (one `INSERT … ON CONFLICT DO UPDATE`) | UNIQUE `(project_id, snapshot_date)` | `progress_snapshots`, `audit_events` `snapshot.run` |

Progress is never stored on the project: it is `SUM(weight × progress_pct) / SUM(weight)` over non-cancelled tasks in `ProjectPortfolio.sql` and `kpis.sql`. A project with no tasks has progress NULL, shown as "No tasks".

## 4. Test scenarios
| Type | User | Steps | Expected |
|---|---|---|---|
| Happy (manager) | `pm5` | PRJ-008 → Tasks → New task, assignee any member, due 2026-12-01 | Task appears; `tasks` row; audit `task.created`. |
| Happy (contributor) | `con5` | PRJ-001 task **#4 Capacity planning** (due 2026-09-10, 60%): My work → progress 100, status Done | Saved; PRJ-001 overdue count 1 → 0 and progress rises (read Overview again; dashboard filtered to PRJ-001 agrees). |
| Validation | `pm5` | New task with title empty, weight 11, progress 101, start after due | 400 with field errors `title`, `weight`, `progressPct`, `dueDate` (shown under the fields). |
| Forbidden role | `con1` | Swagger `POST /api/projects/1/tasks` | **403**. |
| Forbidden (not yours) | `con1` | `PATCH /api/tasks/4/progress` (task is con5's) | **403** "You can only update tasks assigned to you." |
| Out of scope | `con1` | `PATCH /api/tasks/20/progress` (PRJ-002) | **404**. |
| Contributor cancels | `con5` | PATCH task 4 with `status:"cancelled"` | 403 "Only a project manager can cancel or reopen a task." |
| Concurrency | `pm1` | PUT/PATCH a task twice with the same `version` | second call 409 `concurrency_conflict`. |
| Seed edge: due today | `admin1` | PRJ-007 Tasks: task **#85 Network diagram sign-off** due 2026-09-30 | Timing "Due today", **not** Overdue; Tasks with *Overdue only* on PRJ-007 → 0 rows. Dashboard with `asOf=2026-10-01` → it becomes overdue. |
| Seed edge: blocked + overdue | `admin1` | PRJ-006 Tasks | #71 and #72 Blocked and Overdue (2026-08-15, 2026-08-25); PRJ-006 health Amber/Red per overdue count (2 overdue → amber). |
| Seed edge: unassigned | `admin1` | `select id from tasks where assignee_id is null` | 2 tasks (PRJ-005, PRJ-009); dashboard "overdue by owner" shows an **Unassigned** bar only if one is overdue. |
| Seed edge: no tasks | `admin1` | PRJ-010 | Progress "No tasks"; progress card "-" with hint "No tasks yet". |
| Milestone overdue | `admin1` | PRJ-006 Milestones | two pending milestones are flagged **Overdue** (due 2026-06-17 and 2026-08-31; the on-hold project never completed them). |
| Milestone complete twice | `pm1` | Complete a milestone, repeat with the same version | second call 409 (`concurrency_conflict` or `already_completed`). |

## 5. SQL to verify
```sql
select id, title, status, progress_pct, completed_at, xmin::text as version from tasks where id = 4;
select action, data from audit_events where entity_type = 'Task' order by id desc limit 5;
-- project progress (must equal the UI)
select sum(weight*progress_pct)::numeric/sum(weight) as progress from tasks where project_id = 1 and status <> 'cancelled';
-- overdue as of the reporting date (due ON the date is not overdue)
select id, title, due_date from tasks where status not in ('done','cancelled') and due_date < '2026-09-30' order by due_date;   -- 8 rows in the fresh seed (task 85 is due today, so it is not here)
select id, title, due_date from tasks where due_date = '2026-09-30' and status not in ('done','cancelled');                         -- task 85
select snapshot_date, progress_pct, tasks_total, tasks_done, actual_spend from progress_snapshots where project_id = 1 order by 1 desc limit 3;
select count(*) from progress_snapshots where snapshot_date = '2026-10-31';  -- unchanged by repeated runs
```

## 6. Interview questions
1. **Where is "done means 100%" enforced?** In `ProjectTask.ApplyProgress` (domain) and by the CHECK `ck_tasks_done_is_100` in the database.
2. **Why is a task due today not overdue?** Definition in the design doc: `due_date < asOf`; `TaskRules` and the SQL use the same rule, with unit tests on the boundary.
3. **How do you stop a contributor editing someone else's task by guessing the id?** Scope check first (404 outside the project), then assignee check (403).
4. **Is progress stored anywhere?** Only in the history table; live progress is calculated by SQL on every read, so the dashboard changes the next time you load it.
5. **What guarantees a task's milestone belongs to the same project?** The service check (400) and a composite foreign key `(milestone_id, project_id)` → `milestones (id, project_id)`.
