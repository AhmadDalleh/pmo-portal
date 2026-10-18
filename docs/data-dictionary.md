# Data dictionary

One section per generated CSV file in `data/csv/` (the file name is the table name). Generated from the live PostgreSQL schema (`information_schema`) after the `ReportingViews` migration; narrative rules and KPI formulas are in `docs/database-design.md`. All data is fictional and reproducible with seed `20260930` (`tools/DataGenerator`). Dates are `date` (business) or `timestamptz` (UTC). Money is `numeric(18,2)` in AED.

Column keys: **PK** primary key, **FK -> table** foreign key (all `ON DELETE RESTRICT`), **NULL** nullable. Unique indexes and CHECK constraints are listed under each table.

## departments

Organisation units used for portfolio filtering (3 rows).

| Column | Type | Key / null | Default |
|---|---|---|---|
| id | integer | PK |  |
| code | varchar(10) |  |  |
| name | varchar(100) |  |  |
| is_active | boolean |  | true |

Unique: `(code)`; `(name)`

## users

Demo people (18 rows: 2 administrators, 5 project managers, 11 contributors). This section describes the **CSV file**, which differs from the database table: the `users` table is the ASP.NET Identity table (it also has user_name, normalized_email, password_hash, security_stamp, lockout columns). The importer creates those, hashes the password from `DEMO_USER_PASSWORD` (never stored in the CSV or in Git) and assigns the Identity role from the `role` column.

| CSV column | Type | Key / null | Notes |
|---|---|---|---|
| id | integer | PK | |
| email | varchar(256) | unique | fictional `@pmo.example` address; also the sign-in name |
| full_name | varchar(150) | | fictional (Bogus, seed 20260930) |
| department_id | integer | FK -> departments | |
| job_title | varchar(100) | NULL | |
| is_active | boolean | | inactive users cannot sign in |
| role | text | | `Administrator`, `ProjectManager` or `Contributor`; one role per user |

Database-only columns on `users`: Identity fields listed above. Unique: `(normalized_user_name)`.

## projects

Portfolio projects with owner, department, lifecycle status and dates (10 rows).

| Column | Type | Key / null | Default |
|---|---|---|---|
| id | integer | PK |  |
| code | varchar(20) |  |  |
| name | varchar(200) |  |  |
| description | text | NULL |  |
| department_id | integer | FK -> departments |  |
| owner_id | integer | FK -> users |  |
| status | varchar(20) |  |  |
| start_date | date |  |  |
| planned_end_date | date |  |  |
| actual_end_date | date | NULL |  |
| created_at | timestamptz |  | now() |
| created_by_id | integer | FK -> users |  |
| updated_at | timestamptz | NULL |  |
| updated_by_id | integer | FK -> users, NULL |  |

Unique: `(code)`

Checks: `CHECK ((((status)::text <> 'completed'::text) OR (actual_end_date IS NOT NULL)))`; `CHECK ((planned_end_date >= start_date))`; `CHECK (((status)::text = ANY ((ARRAY['proposed'::character varying, 'active'::character varying, 'on_hold'::character varying, 'completed'::character varying, 'cancelled'::character varying])::text[])))`

## project_members

Who belongs to which project and in what project role; drives access scope.

| Column | Type | Key / null | Default |
|---|---|---|---|
| project_id | integer | PK, FK -> projects |  |
| user_id | integer | PK, FK -> users |  |
| member_role | varchar(20) |  |  |
| added_at | timestamptz |  | now() |

Checks: `CHECK (((member_role)::text = ANY ((ARRAY['manager'::character varying, 'contributor'::character varying])::text[])))`

## tasks

Work items with assignee (nullable = unassigned), weight and progress; basis of project progress.

| Column | Type | Key / null | Default |
|---|---|---|---|
| id | integer | PK |  |
| project_id | integer | FK -> projects |  |
| milestone_id | integer | FK -> milestones, NULL |  |
| title | varchar(200) |  |  |
| description | text | NULL |  |
| assignee_id | integer | FK -> users, NULL |  |
| status | varchar(20) |  |  |
| priority | varchar(10) |  |  |
| start_date | date | NULL |  |
| due_date | date |  |  |
| weight | smallint |  | 1 |
| progress_pct | smallint |  | 0 |
| completed_at | timestamptz | NULL |  |
| created_at | timestamptz |  | now() |
| created_by_id | integer | FK -> users |  |
| updated_at | timestamptz | NULL |  |
| updated_by_id | integer | FK -> users, NULL |  |

Checks: `CHECK (((start_date IS NULL) OR (due_date >= start_date)))`; `CHECK ((((status)::text <> 'done'::text) OR (progress_pct = 100)))`; `CHECK (((priority)::text = ANY ((ARRAY['low'::character varying, 'medium'::character varying, 'high'::character varying, 'critical'::character varying])::text[])))`; `CHECK (((progress_pct >= 0) AND (progress_pct <= 100)))`; `CHECK (((status)::text = ANY ((ARRAY['todo'::character varying, 'in_progress'::character varying, 'blocked'::character varying, 'done'::character varying, 'cancelled'::character varying])::text[])))`; `CHECK (((weight >= 1) AND (weight <= 10)))`

## milestones

Dated delivery checkpoints per project.

| Column | Type | Key / null | Default |
|---|---|---|---|
| id | integer | PK |  |
| project_id | integer | FK -> projects |  |
| name | varchar(200) |  |  |
| description | text | NULL |  |
| due_date | date |  |  |
| status | varchar(20) |  |  |
| completed_at | date | NULL |  |
| created_at | timestamptz |  | now() |
| created_by_id | integer | FK -> users |  |
| updated_at | timestamptz | NULL |  |
| updated_by_id | integer | FK -> users, NULL |  |

Unique: `(id, project_id)`

Checks: `CHECK ((((status)::text = 'completed'::text) = (completed_at IS NOT NULL)))`; `CHECK (((status)::text = ANY ((ARRAY['pending'::character varying, 'completed'::character varying])::text[])))`

## risks

Risks and issues; `score` = probability x impact (generated column), high exposure = 15 or more.

| Column | Type | Key / null | Default |
|---|---|---|---|
| id | integer | PK |  |
| project_id | integer | FK -> projects |  |
| type | varchar(10) |  |  |
| title | varchar(200) |  |  |
| description | text | NULL |  |
| owner_id | integer | FK -> users |  |
| probability | smallint |  |  |
| impact | smallint |  |  |
| score | smallint |  | generated |
| mitigation | text | NULL |  |
| due_date | date | NULL |  |
| status | varchar(20) |  |  |
| closed_at | timestamptz | NULL |  |
| created_at | timestamptz |  | now() |
| created_by_id | integer | FK -> users |  |
| updated_at | timestamptz | NULL |  |
| updated_by_id | integer | FK -> users, NULL |  |

Checks: `CHECK ((((status)::text = 'closed'::text) = (closed_at IS NOT NULL)))`; `CHECK (((impact >= 1) AND (impact <= 5)))`; `CHECK (((probability >= 1) AND (probability <= 5)))`; `CHECK (((status)::text = ANY ((ARRAY['open'::character varying, 'mitigating'::character varying, 'closed'::character varying])::text[])))`; `CHECK (((type)::text = ANY ((ARRAY['risk'::character varying, 'issue'::character varying])::text[])))`

## change_requests

Project change requests and their workflow status.

| Column | Type | Key / null | Default |
|---|---|---|---|
| id | integer | PK |  |
| code | varchar(20) |  |  |
| project_id | integer | FK -> projects |  |
| title | varchar(200) |  |  |
| description | text |  |  |
| type | varchar(20) |  |  |
| cost_impact | numeric(18,2) | NULL |  |
| schedule_impact_days | integer | NULL |  |
| status | varchar(20) |  |  |
| requested_by_id | integer | FK -> users |  |
| submitted_at | timestamptz | NULL |  |
| decided_by_id | integer | FK -> users, NULL |  |
| decided_at | timestamptz | NULL |  |
| decision_reason | text | NULL |  |
| created_at | timestamptz |  | now() |
| created_by_id | integer | FK -> users |  |
| updated_at | timestamptz | NULL |  |
| updated_by_id | integer | FK -> users, NULL |  |

Unique: `(code)`

Checks: `CHECK ((((status)::text <> ALL ((ARRAY['approved'::character varying, 'rejected'::character varying, 'returned'::character varying])::text[])) OR ((decided_by_id IS NOT NULL) AND (decided_at IS NOT NULL) AND (length(btrim(decision_reason)) > 0))))`; `CHECK (((decided_by_id IS NULL) OR (decided_by_id <> requested_by_id)))`; `CHECK (((status)::text = ANY ((ARRAY['draft'::character varying, 'submitted'::character varying, 'returned'::character varying, 'approved'::character varying, 'rejected'::character varying, 'withdrawn'::character varying])::text[])))`; `CHECK (((type)::text = ANY ((ARRAY['scope'::character varying, 'schedule'::character varying, 'budget'::character varying, 'resource'::character varying])::text[])))`

## change_request_history

Append-only transition history, one row per status change with actor, reason and time.

| Column | Type | Key / null | Default |
|---|---|---|---|
| id | bigint | PK |  |
| change_request_id | integer | FK -> change_requests |  |
| from_status | varchar(20) | NULL |  |
| to_status | varchar(20) |  |  |
| actor_id | integer | FK -> users |  |
| reason | text | NULL |  |
| occurred_at | timestamptz |  | now() |
| correlation_id | varchar(64) | NULL |  |

## project_budgets

Initial approved budget per project (one row per project).

| Column | Type | Key / null | Default |
|---|---|---|---|
| id | integer | PK |  |
| project_id | integer | FK -> projects |  |
| initial_amount | numeric(18,2) |  |  |
| currency | character |  | 'AED'::bpchar |
| approved_by_id | integer | FK -> users |  |
| approved_at | timestamptz |  |  |

Unique: `(project_id)`

Checks: `CHECK ((currency = 'AED'::bpchar))`; `CHECK ((initial_amount >= (0)::numeric))`

## budget_revisions

Dated budget changes; only `approved` ones count towards the current budget.

| Column | Type | Key / null | Default |
|---|---|---|---|
| id | integer | PK |  |
| project_id | integer | FK -> projects |  |
| amount | numeric(18,2) |  |  |
| reason | text |  |  |
| effective_date | date |  |  |
| status | varchar(20) |  |  |
| change_request_id | integer | FK -> change_requests, NULL |  |
| requested_by_id | integer | FK -> users |  |
| requested_at | timestamptz |  |  |
| decided_by_id | integer | FK -> users, NULL |  |
| decided_at | timestamptz | NULL |  |
| decision_reason | text | NULL |  |

Checks: `CHECK ((amount <> (0)::numeric))`; `CHECK (((decided_by_id IS NULL) OR (decided_by_id <> requested_by_id)))`; `CHECK ((((status)::text = 'pending'::text) = (decided_by_id IS NULL)))`; `CHECK (((status)::text = ANY ((ARRAY['pending'::character varying, 'approved'::character varying, 'rejected'::character varying])::text[])))`

## cost_categories

Cost classification (5 rows); categories are deactivated, never deleted.

| Column | Type | Key / null | Default |
|---|---|---|---|
| id | integer | PK |  |
| code | varchar(20) |  |  |
| name | varchar(100) |  |  |
| is_active | boolean |  | true |

Unique: `(code)`; `(name)`

## cost_transactions

Expense records. `draft` costs are editable and excluded from spend; `posted` are immutable (trigger); reversals are posted negative rows.

| Column | Type | Key / null | Default |
|---|---|---|---|
| id | bigint | PK |  |
| project_id | integer | FK -> projects |  |
| category_id | integer | FK -> cost_categories |  |
| txn_date | date |  |  |
| amount | numeric(18,2) |  |  |
| description | varchar(300) |  |  |
| vendor_name | varchar(150) | NULL |  |
| reference_no | varchar(50) | NULL |  |
| status | varchar(10) |  |  |
| reverses_transaction_id | bigint | FK -> cost_transactions, NULL |  |
| idempotency_key | uuid |  |  |
| created_by_id | integer | FK -> users |  |
| created_at | timestamptz |  |  |
| posted_by_id | integer | FK -> users, NULL |  |
| posted_at | timestamptz | NULL |  |

Unique: `(idempotency_key)`; `(reverses_transaction_id)`

Checks: `CHECK ((((reverses_transaction_id IS NULL) AND (amount > (0)::numeric)) OR ((reverses_transaction_id IS NOT NULL) AND (amount < (0)::numeric))))`; `CHECK ((((status)::text = 'posted'::text) = ((posted_by_id IS NOT NULL) AND (posted_at IS NOT NULL))))`; `CHECK (((status)::text = ANY ((ARRAY['draft'::character varying, 'posted'::character varying])::text[])))`

## financial_forecasts

Monthly future estimate per project and category (month start dates).

| Column | Type | Key / null | Default |
|---|---|---|---|
| id | integer | PK |  |
| project_id | integer | FK -> projects |  |
| category_id | integer | FK -> cost_categories |  |
| period_month | date |  |  |
| amount | numeric(18,2) |  |  |
| updated_by_id | integer | FK -> users |  |
| updated_at | timestamptz |  |  |

Unique: `(project_id, category_id, period_month)`

Checks: `CHECK ((amount >= (0)::numeric))`; `CHECK ((EXTRACT(day FROM period_month) = (1)::numeric))`

## progress_snapshots

Daily project progress and spend history for trends.

| Column | Type | Key / null | Default |
|---|---|---|---|
| id | bigint | PK |  |
| project_id | integer | FK -> projects |  |
| snapshot_date | date |  |  |
| progress_pct | numeric(5,2) |  |  |
| tasks_total | integer |  |  |
| tasks_done | integer |  |  |
| actual_spend | numeric(18,2) |  |  |

Unique: `(project_id, snapshot_date)`

## audit_events

Append-only audit trail (actor, action, entity, correlation id, JSON data).

| Column | Type | Key / null | Default |
|---|---|---|---|
| id | bigint | PK |  |
| occurred_at | timestamptz |  | now() |
| actor_id | integer | FK -> users, NULL |  |
| project_id | integer | FK -> projects, NULL |  |
| entity_type | varchar(50) |  |  |
| entity_id | varchar(50) |  |  |
| action | varchar(50) |  |  |
| correlation_id | varchar(64) | NULL |  |
| data | jsonb | NULL |  |

## outbox_messages

Events queued by business transactions for the background worker; `dedup_key` is unique.

| Column | Type | Key / null | Default |
|---|---|---|---|
| id | bigint | PK |  |
| type | varchar(100) |  |  |
| payload | jsonb |  |  |
| dedup_key | varchar(200) |  |  |
| status | varchar(20) |  |  |
| occurred_at | timestamptz |  |  |
| available_at | timestamptz |  |  |
| attempts | integer |  | 0 |
| processed_at | timestamptz | NULL |  |
| last_error | text | NULL |  |

Unique: `(dedup_key)`

Checks: `CHECK (((status)::text = ANY ((ARRAY['pending'::character varying, 'processed'::character varying, 'failed'::character varying])::text[])))`

## automation_runs

One row per worker or job run with counts, error and correlation id.

| Column | Type | Key / null | Default |
|---|---|---|---|
| id | bigint | PK |  |
| job_name | varchar(100) |  |  |
| started_at | timestamptz |  |  |
| finished_at | timestamptz | NULL |  |
| status | varchar(20) |  |  |
| messages_processed | integer |  |  |
| messages_failed | integer |  |  |
| error | text | NULL |  |
| correlation_id | varchar(64) | NULL |  |

Checks: `CHECK (((status)::text = ANY ((ARRAY['running'::character varying, 'succeeded'::character varying, 'partial'::character varying, 'failed'::character varying])::text[])))`

## notifications

In-app notifications created by the worker; unique per (source outbox message, user).

| Column | Type | Key / null | Default |
|---|---|---|---|
| id | bigint | PK |  |
| user_id | integer | FK -> users |  |
| title | varchar(200) |  |  |
| body | text |  |  |
| link_url | varchar(300) | NULL |  |
| source_outbox_id | bigint | FK -> outbox_messages |  |
| created_at | timestamptz |  |  |
| read_at | timestamptz | NULL |  |

Unique: `(source_outbox_id, user_id)`
