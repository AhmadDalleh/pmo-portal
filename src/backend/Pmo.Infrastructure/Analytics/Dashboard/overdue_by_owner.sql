-- Overdue open tasks per assignee (same predicate as the KPI and the task drill-down).
SELECT t.assignee_id AS user_id, COALESCE(u.full_name, 'Unassigned') AS owner_name, COUNT(*)::int AS overdue_tasks
FROM tasks t
JOIN fp ON fp.id = t.project_id
LEFT JOIN users u ON u.id = t.assignee_id
WHERE t.status NOT IN ('done', 'cancelled') AND t.due_date < @as_of::date
GROUP BY t.assignee_id, u.full_name
ORDER BY COUNT(*) DESC, owner_name
