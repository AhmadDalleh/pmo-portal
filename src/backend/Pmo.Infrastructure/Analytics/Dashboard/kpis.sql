/*FINANCE*/
, task_k AS (
    SELECT COUNT(*) FILTER (WHERE t.status NOT IN ('done', 'cancelled') AND t.due_date < @as_of::date) AS overdue_tasks,
           COUNT(*) FILTER (WHERE t.status = 'blocked') AS blocked_tasks,
           COALESCE(SUM(t.weight * t.progress_pct) FILTER (WHERE t.status <> 'cancelled'), 0) AS weighted_progress,
           COALESCE(SUM(t.weight) FILTER (WHERE t.status <> 'cancelled'), 0) AS total_weight
    FROM tasks t JOIN fp ON fp.id = t.project_id
),
milestone_k AS (
    SELECT COUNT(*) FILTER (WHERE m.status = 'pending' AND m.due_date BETWEEN @as_of::date AND @as_of::date + 14) AS milestones_due_soon
    FROM milestones m JOIN fp ON fp.id = m.project_id
),
risk_k AS (
    SELECT COUNT(*) FILTER (WHERE r.status IN ('open', 'mitigating') AND r.score >= 15) AS open_high_risks
    FROM risks r JOIN fp ON fp.id = r.project_id
),
cr_k AS (
    SELECT COUNT(*) FILTER (WHERE c.status = 'submitted') AS pending_change_requests
    FROM change_requests c JOIN fp ON fp.id = c.project_id
),
fin AS (
    SELECT COALESCE(SUM(current_budget), 0) AS current_budget,
           COALESCE(SUM(actual_spend), 0) AS actual_spend,
           COALESCE(SUM(forecast_to_complete), 0) AS forecast_to_complete,
           COUNT(*) FILTER (WHERE actual_spend + forecast_to_complete > current_budget) AS overrun_projects,
           COALESCE(SUM(actual_spend + forecast_to_complete - current_budget)
                    FILTER (WHERE actual_spend + forecast_to_complete > current_budget), 0) AS overrun_amount
    FROM per_project
)
SELECT (SELECT COUNT(*) FROM fp)::int AS total_projects,
       (SELECT COUNT(*) FROM fp WHERE status = 'active')::int AS active_projects,
       tk.weighted_progress::numeric / NULLIF(tk.total_weight, 0) AS portfolio_progress_pct,   -- task-weighted, not an average of projects; ::numeric avoids integer division
       tk.overdue_tasks::int AS overdue_tasks,
       mk.milestones_due_soon::int AS milestones_due_soon,
       rk.open_high_risks::int AS open_high_risks,
       tk.blocked_tasks::int AS blocked_tasks,
       ck.pending_change_requests::int AS pending_change_requests,
       fin.current_budget,
       fin.actual_spend,
       fin.current_budget - fin.actual_spend AS remaining_budget,
       fin.actual_spend / NULLIF(fin.current_budget, 0) AS utilization,
       fin.forecast_to_complete,
       fin.actual_spend + fin.forecast_to_complete AS eac,
       fin.current_budget - (fin.actual_spend + fin.forecast_to_complete) AS projected_variance,
       fin.overrun_projects::int AS projected_overrun_projects,
       fin.overrun_amount AS projected_overrun_amount
FROM task_k tk, milestone_k mk, risk_k rk, cr_k ck, fin
