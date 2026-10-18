-- Open tasks behind "overdue tasks" (overdue = true) or all open tasks. Optional single assignee or unassigned only.
SELECT t.id, fp.code AS project_code, t.title, COALESCE(u.full_name, 'Unassigned') AS assignee_name, t.status, t.priority, t.due_date,
       GREATEST(@as_of::date - t.due_date, 0)::int AS days_overdue, t.progress_pct::int AS progress_pct,
       COUNT(*) OVER ()::int AS total_count
FROM tasks t
JOIN fp ON fp.id = t.project_id
LEFT JOIN users u ON u.id = t.assignee_id
WHERE t.status NOT IN ('done', 'cancelled')
  AND (@overdue::boolean IS NOT TRUE OR t.due_date < @as_of::date)
  AND (@assignee_id::int IS NULL OR t.assignee_id = @assignee_id::int)
  AND (@unassigned::boolean IS NOT TRUE OR t.assignee_id IS NULL)
ORDER BY t.due_date, t.id
LIMIT @page_size OFFSET @offset
