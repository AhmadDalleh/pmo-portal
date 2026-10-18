# Slice 06 — Change requests, outbox worker, notifications, risks, admin (E1–E5, H1–H3, D1–D2, A4–A6)

## What changed
- **Domain**: `ChangeRequest` state machine (`Submit`, `Withdraw`, `Decide`, `EnsureEditableBy`, `CreationHistory`), `Risk.ApplyStatus / IsHighExposure`.
- **Application**: `Features/ChangeRequests` (`ChangeRequestService`), `Features/Risks`, `Features/Notifications`, `Features/Admin` (`AdminService`, `IUserAdmin`).
- **Infrastructure**: `Outbox/` (`OutboxBatchRunner`, `NotificationBuilder`, `OutboxProcessor`, `DailySnapshotJob`), `Identity/UserAdminService`.
- **Api**: `ChangeRequestsController` (incl. approvals), `RisksController`, `NotificationsController`, `AdminController`.
- **Generator**: seed audit actions now use the same names as the API (`change_request.decided`, `budget_revision.decided`).
- **Tests**: +20 unit, +34 integration. Total 51 unit + 170 integration, all passing.

## Request flow (approve a change request)
Angular approvals page → confirm dialog → `ApprovalsApi.decide` → `POST /api/approvals/change-requests/{id}/decision` → `[Authorize(CanDecide)]` → `ChangeRequestService.DecideAsync` → `EnsureCanManageAsync` (404 / 403) → **transaction**: `cr.Decide()` (state machine, self-approval rule) + 1 `change_request_history` row + `audit_events` row + `outbox_messages` row (`cr-approved:{id}`) → commit.
Later, the `OutboxProcessor` (every 10 s) → `OutboxBatchRunner` → `notifications` rows for requester and project owner → bell icon on their next fetch.

## Key decisions
- **The state machine lives on the entity** (`Allowed` table + `MoveTo`). `Decide` returns the single history row, so it is impossible to change status without producing exactly one history entry.
- **Outbox pattern**: the business transaction only writes a message; sending (here: creating notifications) happens later and can fail and retry without ever undoing the approval.
- **Worker safety**: `FOR UPDATE SKIP LOCKED` (two workers never take one row), `ON CONFLICT DO NOTHING` on `(source_outbox_id, user_id)` (a second delivery is impossible), backoff `2^attempts` minutes, `failed` after 5 attempts with `last_error`, admin retry.
- **Idle polls write no `automation_runs` row**, otherwise the table would get 8,640 empty rows a day.
- **CR codes** are `CR-{id:D4}`. The database assigns the id, so create inserts with a temporary code and renames it in the same transaction.
- **Risk "open"** = open or mitigating, so the default view never shows closed items; `closed_at` is stamped/cleared by `ApplyStatus` because the DB CHECK needs both together.
- **Admin rules** (my additions, flagged): nobody changes their own role or deactivates themselves (lock-out protection); a Project Manager who still owns projects cannot be demoted; demoting removes manager memberships; deactivating revokes refresh tokens.

## Rules and where they are enforced
| Rule | Where |
|---|---|
| Contributor cannot decide | policy `CanDecide` (403) |
| Requester never decides own | `ChangeRequest.Decide` (403 `self_approval_forbidden`) + CHECK `decided_by_id <> requested_by_id` |
| PM decides only for projects they manage | `EnsureCanManageAsync` (404 / 403) |
| Only valid transitions; terminal states | entity (409 `invalid_transition`) |
| Edit only draft/returned, only by requester | `EnsureEditableBy` (403 `only_requester`, 409 `not_editable`) |
| Reason required; stale version | validator + entity (400), `xmin` (409) |
| Exactly one history row, audit, outbox, atomic | explicit transaction in `DecideAsync` (test forces a failure) |
| Approval never changes budget | no code path; test asserts current budget unchanged |
| Score >= 15 needs mitigation | `CreateRiskValidator` (400) |
| Notification once per user and message | UNIQUE index + `ON CONFLICT` |

## Transaction behaviour
Decide: change request update + history + audit + outbox in one transaction. A test installs a trigger that makes the outbox insert fail: the response is 500, the status is still `submitted`, no history row and no audit row exist. Worker: the whole batch (notifications, message status, run row) is one transaction; a message that throws is counted and rescheduled, the rest of the batch still commits.

## How to demo (2 min)
1. `con1` (member of PRJ-001): `POST /api/projects/1/change-requests`, then `POST /api/change-requests/{id}/submit`.
2. `con1` tries `POST /api/approvals/change-requests/{id}/decision` → 403. `pm1` (owner) approves with a reason → 200. In psql: `select * from change_request_history where change_request_id = <id>` (3 rows), `select * from outbox_messages order by id desc limit 1`.
3. Wait up to 10 s (or call nothing: the worker runs by itself). `con1`: `GET /api/notifications` shows the approval; `GET /api/notifications/unread-count` is 1.
4. `admin1`: `GET /api/admin/automation/runs` shows the run; `GET /api/admin/automation/outbox?status=failed` shows the seeded failed alert; `POST .../retry` puts it back in the queue.
5. `pm1` approves their own request → 403 `self_approval_forbidden`; `update change_requests set decided_by_id = requested_by_id ...` in psql → check violation.

## Likely interview questions
1. **Why an outbox instead of creating the notification in the approval request?** The approval must commit even if notification logic breaks; the outbox row is written atomically with it, delivery is retried independently.
2. **How do you stop two workers sending the same notification?** `SKIP LOCKED` at selection and a UNIQUE index with `ON CONFLICT DO NOTHING` at insert; a test replays a processed message.
3. **What guarantees one history row per decision?** `Decide` returns it and the service adds it to the same unit of work; a failure rolls back everything; the test counts rows before and after.
4. **Where is self-approval blocked?** Entity (403), and the DB CHECK constraint if someone bypasses the app.
5. **What happens when the worker crashes mid-batch?** The transaction rolls back, the rows stay `pending`, the next pass retries; notifications cannot duplicate because of the unique index.

## Live-change rehearsal
"Fail after 3 attempts instead of 5": `OutboxBatchRunner.MaxAttempts = 3` (and in `OutboxTests` change the loop `attempt <= 5` to `<= 3` and `attempts` expectation to 3).
