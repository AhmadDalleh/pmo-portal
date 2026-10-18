# API routes

Base path `/api`. All routes need `Authorization: Bearer <accessToken>` unless marked **anon**. Errors are RFC 7807 `ProblemDetails`
with `code` (stable business-rule id), `errors` (field → messages, validation only) and `correlationId`.
Policies: **PM** = Project Manager or Administrator (role policy only; the service then checks the project scope), **ADM** = Administrator.
Out-of-scope projects return 404, visible-but-not-allowed returns 403.

## Health and auth (slice 3)
| Method | Route | Who | Success | Notes |
|---|---|---|---|---|
| GET | `/health` | anon | 200 | 503 when PostgreSQL is down |
| POST | `/auth/login` | anon | 200 | body `{email,password}`; sets HttpOnly refresh cookie; 401 generic on any failure; rate limited |
| POST | `/auth/refresh` | anon (cookie) | 200 | rotates the refresh cookie; 401 if missing, used, expired or revoked |
| POST | `/auth/logout` | anon (cookie) | 204 | revokes the refresh token |
| GET | `/auth/me` | any | 200 | profile + `managedProjectIds` |

## Projects and lookups (slice 3)
| Method | Route | Who | Success | Errors |
|---|---|---|---|---|
| GET | `/projects?search&departmentId&ownerId&status&from&to&sort&dir&page&pageSize&asOf` | any (scope) | 200 paged | 400 unknown sort/status/page |
| GET | `/projects/{id}` | any (scope) | 200 | 404 |
| GET | `/projects/{id}/summary?asOf` | any (scope) | 200 | 404 |
| POST | `/projects` | PM | 201 + Location | 400 `owner_required`/`owner_not_project_manager`/validation, 403, 409 `duplicate_project_code` |
| PUT | `/projects/{id}` | PM + manages project | 200 | 400, 403, 404, 409 `concurrency_conflict` |
| GET | `/projects/{id}/members` | any (scope) | 200 | 404 |
| POST | `/projects/{id}/members` | PM + manages project | 201 | 400 `manager_must_be_pm`/`invalid_member`, 403, 404, 409 `already_member` |
| DELETE | `/projects/{id}/members/{userId}` | PM + manages project | 204 | 409 `cannot_remove_owner` / `member_has_open_tasks` |
| GET | `/lookups/departments` · `/lookups/users?role=` · `/lookups/cost-categories` | any | 200 | |

Sort keys: `code, name, department, owner, status, start, end, progress, utilization`; `dir` = `asc|desc`; default page 1, size 20 (max 100).

## Tasks, milestones, progress (slice 4)
| Method | Route | Who | Success | Errors |
|---|---|---|---|---|
| GET | `/projects/{id}/tasks?status&assigneeId&priority&overdue&sort&dir&page&pageSize&asOf` | any (scope) | 200 paged | 400, 404 |
| GET | `/me/tasks?status&page&pageSize&asOf` | any | 200 paged | status null = open tasks, `all` = everything |
| GET | `/tasks/{id}` | any (scope) | 200 | 404 |
| POST | `/projects/{id}/tasks` | PM + manages project | 201 | 400 `assignee_not_member`/`milestone_not_in_project`/validation, 403, 404 |
| PUT | `/tasks/{id}` | PM + manages project | 200 | 400, 403, 404, 409 `concurrency_conflict` |
| PATCH | `/tasks/{id}/progress` `{progressPct,status,version}` | manager of project, or the task assignee | 200 | 403 (not yours / contributor cancelling), 404, 409 |
| GET | `/projects/{id}/milestones?asOf` | any (scope) | 200 | 404 |
| POST | `/projects/{id}/milestones` | PM + manages project | 201 | 400, 403 |
| PUT | `/milestones/{id}` | PM + manages project | 200 | 409 |
| POST | `/milestones/{id}/complete` `{version}` | PM + manages project | 200 | 409 `already_completed` |
| GET | `/projects/{id}/progress-history` | any (scope) | 200 | 404 |
| POST | `/admin/snapshots/run?date` | ADM | 200 | 403; idempotent upsert |

Task sort keys: `due, title, status, priority, progress, weight`. Task `indicator`: `overdue`, `due_today`, `due_soon` (within 14 days) or empty.

## Finance (slice 5)
`CanPostCosts` = Project Manager or Administrator role policy; every write then also needs management of the project.
| Method | Route | Who | Success | Errors |
|---|---|---|---|---|
| GET | `/projects/{id}/finance/summary?asOf` | any (scope) | 200 | 404 |
| GET | `/projects/{id}/finance/by-category?asOf` | any (scope) | 200 | |
| GET | `/projects/{id}/finance/monthly?asOf&categoryId` | any (scope) | 200 | |
| GET | `/projects/{id}/costs?status&categoryId&from&to&page&pageSize` | any (scope) | 200 paged | 400, 404 |
| GET | `/finance/costs/{id}` | any (scope) | 200 | 404 |
| POST | `/projects/{id}/costs` + header `Idempotency-Key: <guid>` | CanPostCosts | 201 (200 with the original on a repeat) | 400 `idempotency_key_required`/`category_not_found`/validation, 403, 404, 409 `idempotency_key_reused` |
| PUT | `/finance/costs/{id}` | CanPostCosts | 200 | 409 `posted_immutable` / `concurrency_conflict` |
| DELETE | `/finance/costs/{id}` | CanPostCosts | 204 | 409 `posted_immutable` |
| POST | `/finance/costs/{id}/post` `{version}` | CanPostCosts | 200 | 403, 404, 409 `already_posted` / `concurrency_conflict` |
| POST | `/finance/costs/{id}/reverse` `{reason}` | CanPostCosts | 201 | 409 `already_reversed` / `not_posted` / `cannot_reverse_reversal` |
| GET | `/projects/{id}/budget-revisions` | any (scope) | 200 | |
| POST | `/projects/{id}/budget-revisions` | PM + manages project | 201 | 400 `budget_would_be_negative` / `change_request_*`, 403 |
| POST | `/finance/budget-revisions/{id}/decision` `{decision: approved or rejected, reason, version}` | ADM | 200 | 403 `self_approval_forbidden`, 409 `invalid_transition` |
| GET / PUT | `/projects/{id}/forecasts` (PUT = whole grid, upsert) | read: any (scope); write: PM + manages | 200 | 400 `forecast_month_not_future` / `duplicate_forecast_cell` |
| GET / POST / PUT | `/finance/cost-categories` (writes ADM) | any / ADM | 200 / 201 | 409 `duplicate_category` |

## Change requests, approvals, risks, notifications, admin (slice 6)
| Method | Route | Who | Success | Errors |
|---|---|---|---|---|
| GET | `/projects/{id}/change-requests?status&page&pageSize` | any (scope) | 200 paged | 404 |
| POST | `/projects/{id}/change-requests` | any project member (or ADM) | 201 | 400, 404 |
| GET | `/change-requests/{id}` | any (scope) | 200 | 404 |
| PUT | `/change-requests/{id}` | requester, draft/returned only | 200 | 403 `only_requester`, 409 `not_editable` / `concurrency_conflict` |
| POST | `/change-requests/{id}/submit` `{version}` | requester | 200 | 409 `invalid_transition` |
| POST | `/change-requests/{id}/withdraw` `{reason?, version}` | requester | 200 | 409 `invalid_transition` |
| GET | `/change-requests/{id}/history` | any (scope) | 200 | 404 |
| GET | `/approvals/change-requests?status=submitted&projectId&page` | PM / ADM | 200 paged | 403 (contributor) |
| POST | `/approvals/change-requests/{id}/decision` `{decision: approve or reject or return, reason, version}` | PM managing the project / ADM | 200 | 403 (contributor, `self_approval_forbidden`, not managing), 404, 409 |
| GET | `/risks?highExposure&status=open or closed or all&projectId&probability&impact&page` | any (scope) | 200 paged | 400 |
| GET / POST | `/projects/{id}/risks` | read: any (scope); write: PM + manages | 200 / 201 | 400 (mitigation required, owner not member), 403 |
| PUT | `/risks/{id}` | PM + manages project | 200 | 409 |
| GET | `/notifications?unread&page&pageSize` · `/notifications/unread-count` | any (own) | 200 | |
| POST | `/notifications/{id}/read` | recipient | 200 | 404 (not yours) |
| GET | `/admin/users` · PUT `/admin/users/{id}/role` `{role}` · PUT `/admin/users/{id}/status` `{isActive}` | ADM | 200 | 409 `cannot_change_own_role` / `user_owns_projects` / `cannot_deactivate_self` |
| GET | `/admin/audit-events?entityType&action&actorId&projectId&from&to&page` | ADM | 200 paged | |
| GET | `/admin/automation/runs` · `/admin/automation/outbox?status` | ADM | 200 paged | |
| POST | `/admin/automation/outbox/{id}/retry` | ADM | 200 | 409 `not_failed` |

## Dashboard (slice 7, backend)
Any signed-in user; numbers are limited to the caller's projects. Every route takes the same filters:
`departmentId, projectId, managerId (project owner), status, from, to (projects whose start..planned end overlaps), asOf`; `categoryId` only affects
monthly trend, by-category and the cost drill-down (the KPI cards and budget-vs-actual ignore it).
| Method | Route | Returns |
|---|---|---|
| GET | `/dashboard/kpis` | delivery, risk/governance and finance cards (G1) |
| GET | `/dashboard/finance/by-project` | budget, actual, forecast, EAC per project (visual 1) |
| GET | `/dashboard/finance/monthly` | monthly actual + forecast (visual 2) |
| GET | `/dashboard/finance/by-category` | spend and forecast per category (visual 3) |
| GET | `/dashboard/delivery/overdue-by-owner` | overdue open tasks per assignee (visual 4) |
| GET | `/dashboard/risks/matrix` | 25 cells with open counts (visual 5) |
| GET | `/dashboard/delivery/project-health` | per project progress, overdue, risks, utilisation, badge (visual 6) |
| GET | `/dashboard/drilldown/costs?page&pageSize` | posted transactions behind actual spend + `totalAmount` (equals the KPI) |
| GET | `/dashboard/drilldown/tasks?overdue=true&assigneeId&unassigned&page&pageSize` | open tasks behind "overdue tasks" |
| GET | `/dashboard/drilldown/risks?probability&impact&highExposure&page&pageSize` | open risks behind a matrix cell / the risk KPI |
