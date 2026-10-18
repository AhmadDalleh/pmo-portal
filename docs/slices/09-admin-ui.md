# Slice 09 - Admin UI (stories A4, A5, H3, F2)

## What was built
No backend change: every endpoint already existed (`docs/api-routes.md`). This slice adds the Angular admin area at `/admin`.

| Tab | Route | Endpoints used | Story |
|---|---|---|---|
| Users | `/admin/users` | `GET /api/admin/users`, `PUT .../role`, `PUT .../status` | A4 |
| Audit log | `/admin/audit` | `GET /api/admin/audit-events` (filters + paging) | A5 |
| Automation | `/admin/automation` | `GET /api/admin/automation/outbox`, `POST .../outbox/{id}/retry`, `GET /api/admin/automation/runs` | H3 |
| Cost categories | `/admin/categories` | `GET/POST/PUT /api/finance/cost-categories` | F2 |

Files (all under `src/frontend/pmo-web/src/app/`):
- `core/auth/auth.guard.ts` - new `adminGuard` (non-admin is redirected to `/projects`).
- `app.ts`, `app.routes.ts` - Admin menu item only when `auth.isAdmin()`; lazy `/admin` route with child routes.
- `core/api/admin.api.ts`, `core/models/admin.models.ts` - one API service and its types.
- `features/admin/` - `admin-shell`, `admin-users`, `admin-audit`, `admin-automation`, `admin-categories`, `category-dialog`.
- `shared/data-table` - new optional `detail` template (expandable row). `shared/status-badge` - new statuses (inactive, succeeded, partial, failed, running, processed).

## Request flow (example: change a role)
1. Admin opens the row menu, picks "Make Project Manager".
2. `Confirm.ask` shows the consequences; Cancel stops here.
3. `AdminApi.changeRole` -> `PUT /api/admin/users/{id}/role`.
4. Server checks the policy and the rules (own role, PM owning projects), changes the role, writes the audit event, returns 200 or ProblemDetails.
5. The component shows a toast (success, or the server message with correlation id) and **always reloads the list** - nothing is patched locally.

## Key decisions
- **Hidden and guarded:** menu item hidden for non-admins, route guarded by `adminGuard`. This is only usability; the real protection is the `Administrator` policy on every endpoint.
- **Rules stay on the server.** The UI does not pre-check "own role" or "PM owns projects"; it shows the server's message. One source of truth.
- **Users are filtered and paged in the browser** because the endpoint returns every user (a few dozen). Audit, outbox and runs are paged by the server.
- **Expandable audit row** is a generic `detail` template on `DataTable`, not audit-specific code. Because a second row definition exists, the table uses `multiTemplateDataRows` (without it Angular throws "only one default row" and the table never leaves Loading - this happened during development).
- **Categories cannot be deleted**, only deactivated, because costs may reference them.

## Demo (login admin1)
1. Admin menu -> Users: filter by role/department; try "Make Contributor" on yourself -> confirm -> server message "cannot change your own role".
2. Deactivate a contributor, see the badge change; activate again.
3. Audit log: newest first; click a row to see correlation id and JSON; filter by action `cost.posted` or a date range.
4. Automation: the failed outbox message (BudgetThresholdCrossed, 5 attempts) -> Retry -> confirm; refresh shows it processed and a new OutboxProcessor run.
5. Cost categories: New category `TRAINING`; rename; deactivate with confirm.
6. Log in as `pm1`: no Admin menu; typing `/admin` redirects to Projects.

## Interview questions
1. **Is hiding the menu enough security?** No. The guard and hidden menu are UX; each endpoint has `[Authorize(Policy=...)]`, and non-admin calls return 403 (covered by existing integration tests).
2. **Why not validate "cannot change own role" in Angular?** Duplicated rules drift; the server must enforce it anyway. We display its ProblemDetails message instead.
3. **Why reload after each mutation?** The server may have changed more than we sent (memberships on demotion, worker processing a retried message). Reloading shows the truth.
4. **Why client-side paging for users but server-side for audit?** User count is small and bounded; the audit log grows without limit, so it must be paged and filtered in SQL.
5. **How does the expandable row work?** `DataTable` accepts a `detail` TemplateRef; a second row def renders a full-width row that is hidden unless that row is the expanded one; `multiTemplateDataRows` lets Material render both rows per data item.

## Live-change rehearsal
- Change the users page size from 10 to 5 (`pageSize` signal in `admin-users.ts`).
- Add an "Entity type" default filter in the audit form.
- Change the Retry confirm text in `admin-automation.ts`.

## Tests
No endpoint changed, so no new integration tests. `dotnet test` (unit + 190 integration) and `ng build` pass. UI verified manually in the browser against the real API.
