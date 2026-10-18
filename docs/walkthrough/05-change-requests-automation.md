# 05 — Change requests, approvals, outbox automation, notifications (E1–E5, H1–H3, A6)

## 1. Business goal
Scope, schedule, budget and resource changes need a visible request, a named decision-maker and a reason, kept as history.
A requester must never approve their own request, and an approval must never silently change the budget.
Follow-up work (telling people) runs in the background from an outbox, so a notification problem can never undo an approval.

## 2. UI screens and actions
| Screen | Click | Result |
|---|---|---|
| Project → **Change requests** | **New change request** (any member) | Dialog: title, description, type, cost impact (information only), schedule impact days → saved as **draft**, opens the CR page. |
| CR page (`/change-requests/{id}`) | **Edit / Submit / Withdraw** (requester, draft or returned) | Submit asks for confirmation; Withdraw asks for a reason. |
| CR page | **Approve / Return / Reject** (manager of the project or admin, **not the requester**) | Confirmation dialog with a mandatory reason, then the page reloads: status, decision text and **Timeline** (who, when, from → to, reason). |
| **Approvals** (menu, PM/Admin) | Row icons Approve / Return / Reject / Open | Queue of submitted CRs of projects you manage, oldest first, never your own. |
| Bell icon / **Notifications** | Click a notification | Marks it read and follows the link (e.g. `/change-requests/18`). Unread count refreshes after each navigation. |
| Admin monitor | Swagger only: `GET /api/admin/automation/runs`, `…/outbox?status=failed`, `POST …/outbox/{id}/retry` | See runs and retry failed messages. |

## 3. Per action
| Action | Route | Chain | Rules | Tables / transaction |
|---|---|---|---|---|
| Create | `POST /api/projects/{id}/change-requests` | `ChangeRequestsController` → `ChangeRequestService.CreateAsync` | view scope (members/admin); type in scope/schedule/budget/resource | **tx, two saves**: `change_requests` (temporary code, to get id) → code `CR-{id:D4}`, `change_request_history` (null → draft), `audit_events` `change_request.created` |
| Edit | `PUT /api/change-requests/{id}` | `UpdateAsync` → `ChangeRequest.EnsureEditableBy` | requester only (403 `only_requester`), draft/returned only (409 `not_editable`), `version` | one save + audit |
| Submit | `POST /api/change-requests/{id}/submit` `{version}` | `SubmitAsync` → `ChangeRequest.Submit` | requester; draft or returned → submitted; resubmission clears decision fields | one `SaveChanges`: `change_requests` + **one** `change_request_history` + `audit_events` |
| Withdraw | `POST …/withdraw` `{reason,version}` | `WithdrawAsync` → `Withdraw` | draft/returned → withdrawn (terminal) | same pattern |
| Queue | `GET /api/approvals/change-requests?status&projectId&page` (policy `CanDecide`) | `ReviewQueueAsync` | projects you manage; excludes `requested_by = you` | reads |
| **Decide** | `POST /api/approvals/change-requests/{id}/decision` `{decision:"approve"|"reject"|"return",reason,version}` (policy `CanDecide`) | `DecideAsync` → `ChangeRequest.Decide` | manage scope (404/403); **requester never decides** (403 `self_approval_forbidden` + CHECK); only `submitted` (409 `invalid_transition`); reason required; `version` | **One explicit transaction**: `change_requests` + exactly one `change_request_history` + `audit_events` `change_request.decided` + (on approve) `outbox_messages` `ChangeRequestApproved` (`cr-approved:{id}`). A failure anywhere rolls all back. |
| History | `GET /api/change-requests/{id}/history` | `HistoryAsync` | view scope | reads `change_request_history` (append-only trigger) |
| Worker | background `OutboxProcessor` every 10 s → `OutboxBatchRunner.RunOnceAsync` | `FOR UPDATE SKIP LOCKED` batch of 20 pending → `NotificationBuilder` → `INSERT … ON CONFLICT (source_outbox_id, user_id) DO NOTHING` | failure: attempts+1, `available_at` +2^attempts minutes, `failed` after 5 with `last_error` | **One transaction per pass**: `notifications`, `outbox_messages` status, `automation_runs` row (only when the batch was not empty) |
| Retry | `POST /api/admin/automation/outbox/{id}/retry` (ADM) | `AdminService.RetryAsync` | only `failed` (409 `not_failed`) | `outbox_messages` back to pending, attempts 0 |
| Notifications | `GET /api/notifications`, `…/unread-count`, `POST …/{id}/read` | `NotificationService` | own rows only | `notifications.read_at` |

State machine (`ChangeRequest.Allowed`): draft → submitted | withdrawn; submitted → approved | rejected | returned; returned → submitted | withdrawn. Everything else = 409 `invalid_transition`.

## 4. Test scenarios (fresh seed)
| Type | User | Steps | Expected |
|---|---|---|---|
| Happy (full journey) | `con1` then `pm1` | `con1`: PRJ-001 → New change request ("Test CR") → Submit. `pm1`: Approvals → open it → Approve with reason "OK" | Status approved; timeline 3 entries (draft → submitted → approved); within ~10 s `con1` and `pm1` each get a notification "Change request CR-00xx approved"; bell shows 1. Budget on the Finance tab unchanged. |
| Validation | `con1` | New CR with empty title/description | 400 field errors `title`, `description`. Approve with empty reason → 400 `errors.reason`. |
| Forbidden role | `con1` | Swagger `POST /api/approvals/change-requests/14/decision` | **403**; `select status from change_requests where id=14` → `submitted`; no new history row. |
| Self-approval | `pm1` | Create + submit a CR in PRJ-001 as pm1, then approve it as pm1 | **403** `self_approval_forbidden`. psql: `update change_requests set status='approved', decided_by_id=requested_by_id, decided_at=now(), decision_reason='x' where id=<id>` → check violation `23514`. |
| Not your project | `pm3` | Decide CR-0014 (PRJ-001) | **404**. `pm4` (contributor member of PRJ-001) → **403**. |
| Invalid transition | `pm1` | Decide an already approved CR (e.g. CR-0003) / submit it | 409 `invalid_transition`. |
| Stale version | `pm1` | Decide with an old `version` | 409 `concurrency_conflict`. |
| Seed: full cycle | `admin1` | Open **CR-0002** (PRJ-002) | Timeline: draft → submitted → returned → submitted → approved (5 rows, reasons shown); linked to budget revision #2 (+300,000). |
| Seed: returned/rejected/withdrawn | `admin1` | CR-0007 returned, CR-0006 rejected, CR-0009 withdrawn, CR-0010 withdrawn after return | statuses and timelines as listed. `pm1` (owner) sees CR-0014 in Approvals (submitted by con5). |
| Seed: pending outbox | `admin1` | After importing, **before** the API runs: `select * from outbox_messages where status='pending'` → id 9 (`cr-approved:18`). Start/leave the API running 10 s | message becomes `processed`; CR-0018's requester (con10) and PRJ-007 owner (pm4) get notifications. |
| Seed: failed message | `admin1` | `GET /api/admin/automation/outbox?status=failed` → id 7 (`budget:9:100`, attempts 5). `POST …/7/retry` | status pending, attempts 0; worker processes it and notifies pm1 (owner of PRJ-009). |
| Idempotent delivery | – | In psql set a processed message back to `pending` (see SQL) | After the next pass the notification count for that message is unchanged (UNIQUE + ON CONFLICT). |

## 5. SQL to verify
```sql
-- one history row per transition
select id, from_status, to_status, actor_id, reason, occurred_at from change_request_history where change_request_id = 14 order by id;
select id, code, status, requested_by_id, decided_by_id, decision_reason from change_requests where id = 14;
select action, entity_id, data from audit_events where entity_type = 'ChangeRequest' order by id desc limit 5;
-- outbox and worker
select id, type, dedup_key, status, attempts, available_at, last_error from outbox_messages order by id desc limit 10;
select id, job_name, status, messages_processed, messages_failed, started_at from automation_runs order by id desc limit 5;
select id, user_id, title, read_at from notifications order by id desc limit 5;
-- replay a processed message and prove no duplicates
update outbox_messages set status='pending', processed_at=null where id = 1;
-- (wait 10 s)
select source_outbox_id, user_id, count(*) from notifications group by 1,2 having count(*) > 1;   -- 0 rows
-- the DB-level rule
select conname from pg_constraint where conrelid = 'change_requests'::regclass and contype='c';
```

## 6. Interview questions
1. **What is atomic in a decision and what if the outbox insert fails?** Status, one history row, audit and the outbox message commit together; if any insert fails the transaction rolls back and the request is still `submitted` (a test installs a failing trigger to prove it).
2. **Why an outbox instead of creating the notification inside the request?** The decision must not depend on notification logic; the message is saved atomically and delivered later with retries.
3. **How do two workers not send twice?** `SELECT … FOR UPDATE SKIP LOCKED` per batch, and the unique `(source_outbox_id, user_id)` index with `ON CONFLICT DO NOTHING`.
4. **How is self-approval prevented?** Entity rule (`Decide`) returns 403; the CHECK `decided_by_id <> requested_by_id` stops it even if someone bypasses the app.
5. **Why does approving a CR not change the budget?** Money changes only through a separate, explicitly linked budget revision that an administrator approves (E5).
