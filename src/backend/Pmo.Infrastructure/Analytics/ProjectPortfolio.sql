-- Portfolio list and project KPIs (stories B3, B4, F1). Definitions: docs/database-design.md section 6.
-- Parameters: @as_of date, @scope_ids int[] (the caller's accessible projects - enforced by the service),
--             filters (NULL = no filter), @page_size, @offset.
-- Every one-to-many table (tasks, risks, change requests, revisions, costs, forecasts) is aggregated to
-- one row per project in its own CTE BEFORE joining, so nothing is counted twice (section 7).
WITH progress AS (
    -- Project progress % = SUM(weight * progress_pct) / SUM(weight) over non-cancelled tasks; no tasks => NULL.
    SELECT project_id,
           SUM(weight * progress_pct)::numeric / NULLIF(SUM(weight), 0) AS progress_pct
    FROM tasks
    WHERE status <> 'cancelled' AND project_id = ANY(@scope_ids)
    GROUP BY project_id
),
overdue AS (
    -- A task due ON the as-of date is "due today", not overdue.
    SELECT project_id, COUNT(*) AS overdue_tasks
    FROM tasks
    WHERE due_date < @as_of AND status NOT IN ('done', 'cancelled') AND project_id = ANY(@scope_ids)
    GROUP BY project_id
),
high_risk AS (
    -- Closed risks never count, whatever their score.
    SELECT project_id, COUNT(*) AS open_high_risks
    FROM risks
    WHERE status IN ('open', 'mitigating') AND score >= 15 AND project_id = ANY(@scope_ids)
    GROUP BY project_id
),
pending_cr AS (
    SELECT project_id, COUNT(*) AS pending_change_requests
    FROM change_requests
    WHERE status = 'submitted' AND project_id = ANY(@scope_ids)
    GROUP BY project_id
),
budget AS (
    -- Current approved budget = initial + approved revisions already effective at the as-of date.
    SELECT pb.project_id,
           pb.initial_amount
         + COALESCE(SUM(br.amount) FILTER (WHERE br.status = 'approved' AND br.effective_date <= @as_of), 0) AS current_budget
    FROM project_budgets pb
    LEFT JOIN budget_revisions br ON br.project_id = pb.project_id
    WHERE pb.project_id = ANY(@scope_ids)
    GROUP BY pb.project_id, pb.initial_amount
),
actual AS (
    -- Posted only, dated on or before the as-of date. Reversals are posted negatives, so they net out.
    SELECT project_id, SUM(amount) AS actual_spend
    FROM cost_transactions
    WHERE status = 'posted' AND txn_date <= @as_of AND project_id = ANY(@scope_ids)
    GROUP BY project_id
),
ftc AS (
    -- Forecast to complete = forecasts for months after the as-of month.
    SELECT project_id, SUM(amount) AS forecast_to_complete
    FROM financial_forecasts
    WHERE period_month > date_trunc('month', @as_of::date)::date AND project_id = ANY(@scope_ids)
    GROUP BY project_id
),
kpi AS (
    SELECT p.id, p.code, p.name, d.name AS department_name, u.full_name AS owner_name, p.status,
           p.start_date, p.planned_end_date, p.department_id, p.owner_id,
           pr.progress_pct,
           COALESCE(o.overdue_tasks, 0)::int AS overdue_tasks,
           COALESCE(hr.open_high_risks, 0)::int AS open_high_risks,
           COALESCE(pc.pending_change_requests, 0)::int AS pending_change_requests,
           b.current_budget,
           COALESCE(a.actual_spend, 0) AS actual_spend,
           b.current_budget - COALESCE(a.actual_spend, 0) AS remaining_budget,
           COALESCE(a.actual_spend, 0) / NULLIF(b.current_budget, 0) AS utilization,
           COALESCE(f.forecast_to_complete, 0) AS forecast_to_complete,
           COALESCE(a.actual_spend, 0) + COALESCE(f.forecast_to_complete, 0) AS eac,
           b.current_budget - (COALESCE(a.actual_spend, 0) + COALESCE(f.forecast_to_complete, 0)) AS projected_variance,
           (f.project_id IS NULL) AS no_forecast
    FROM projects p
    JOIN departments d ON d.id = p.department_id
    JOIN users u ON u.id = p.owner_id
    JOIN budget b ON b.project_id = p.id
    LEFT JOIN progress pr ON pr.project_id = p.id
    LEFT JOIN overdue o ON o.project_id = p.id
    LEFT JOIN high_risk hr ON hr.project_id = p.id
    LEFT JOIN pending_cr pc ON pc.project_id = p.id
    LEFT JOIN actual a ON a.project_id = p.id
    LEFT JOIN ftc f ON f.project_id = p.id
    WHERE p.id = ANY(@scope_ids)
      AND (@search::text IS NULL OR p.code ILIKE '%' || @search::text || '%' OR p.name ILIKE '%' || @search::text || '%')
      AND (@department_id::int IS NULL OR p.department_id = @department_id::int)
      AND (@owner_id::int IS NULL OR p.owner_id = @owner_id::int)
      AND (@status::text IS NULL OR p.status = @status::text)
      AND (@from::date IS NULL OR p.planned_end_date >= @from::date)
      AND (@to::date IS NULL OR p.start_date <= @to::date)
)
SELECT kpi.*,
       (utilization >= 0.90 AND utilization <= 1.00) AS near_budget,   -- alert: near budget
       (actual_spend > current_budget) AS overspent,                   -- alert: overspent
       (eac > current_budget) AS projected_overrun,                    -- alert: projected overrun
       (current_budget = 0 AND actual_spend > 0) AS no_budget,         -- utilisation undefined
       COUNT(*) OVER () AS total_count
FROM kpi
/*ORDER_BY*/
LIMIT @page_size OFFSET @offset
