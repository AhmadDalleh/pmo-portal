-- Report 1: overdue incomplete tasks, newest problems first (database-design.md section 6).
-- Overdue = not done/cancelled AND due before the reporting date. A task due ON the reporting date is "due today", not overdue.
-- vw_task_facts already carries the flag (reporting date comes from reporting_settings, 2026-09-30 for the baseline).
-- One table only (tasks plus lookups), so no join can multiply rows.
SELECT project_code,
       department,
       title,
       assignee,
       status,
       priority,
       due_date,
       as_of_date - due_date AS days_overdue,
       progress_pct
FROM vw_task_facts
WHERE is_overdue
ORDER BY days_overdue DESC, project_code, task_id;
