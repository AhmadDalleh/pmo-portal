# Automation run evidence (assignment §8.1 and acceptance test G)

Automation: **transactional outbox + background worker** (`OutboxProcessor`, a `BackgroundService`, every 10 s).
Trigger: approving a change request, or a budget crossing 90 % / 100 %, writes an `outbox_messages` row in the same transaction as the business change.
Rule: the worker claims `pending` rows that are due (`FOR UPDATE SKIP LOCKED`), creates in-app notifications for the requester and project owner (`ON CONFLICT DO NOTHING` on `(source_outbox_id, user_id)`), and records a row in `automation_runs`.
Failure: backoff of `2^attempts` minutes, `failed` after 5 attempts with `last_error`; an administrator can retry from the Admin > Automation screen or `POST /api/admin/automation/outbox/{id}/retry`.

## About the seeded failed message (outbox id 7)
It is **not a real defect**. `tools/DataGenerator/Generation/AutomationSeeder.cs` deliberately writes it as `failed`, 5 attempts, `last_error = "Notification channel unavailable (simulated)."`, with a matching `automation_runs` row `seed-run-7` (status `partial`). The worker never claims `failed` rows, so only the admin Retry moves it. The real delivery code has no simulated failure; the failure path is proven by the automated test below. Run `scripts/reset-db.ps1` (or `.sh`) to bring the failed row back before a demo.

## Real run captured 2026-10-18 (fresh `reset-db`, API started, signed in as admin1)
1. After reset: message 9 (`cr-approved:18`) `pending`, message 7 (`budget:9:100`) `failed`, attempts 5.
2. The worker picked up message 9 on its own: run 29, `OutboxProcessor`, status `succeeded`, processed 1, failed 0, correlation id `9083e0e9e44442ff88ac8b25bfaab244`.
3. `GET /api/admin/automation/outbox?status=failed` returned message 7 with the simulated error.
4. `POST /api/admin/automation/outbox/7/retry` returned status `pending`, attempts 0, `lastError` null.
5. About 7 s later run 30 processed it: message 7 `processed`, attempts 1, `processed_at` 2026-10-18 16:17:51; run 30 `succeeded`, correlation id `ba8b0ce3a1824cc1bad08006b707dc4a`.
6. `notifications` holds 3 rows for messages 7 and 9 together (one per recipient, no duplicates).

SQL to reproduce:
```sql
select id, dedup_key, status, attempts, processed_at from outbox_messages where id in (7, 9) order by id;
select id, job_name, started_at, status, messages_processed, messages_failed, correlation_id from automation_runs order by id desc limit 3;
select count(*) from notifications where source_outbox_id in (7, 9);
```

## Automated proof of the safety rules
- `OutboxTests.Worker_TurnsAnApprovedChangeRequestIntoNotificationsForRequesterAndOwner_AndLogsARun`
- `OutboxTests.Worker_ProcessingTheSameMessageTwice_CreatesOneNotificationPerUser` (no duplicate sends)
- `OutboxTests.Worker_ForAMessageThatKeepsFailing_BacksOffThenMarksItFailed_AndAdminCanRetry` (retry and failure behaviour)
- `ChangeRequestTests.Decide_WhenTheOutboxInsertFails_RollsBack...` (decision, history and outbox commit together)
