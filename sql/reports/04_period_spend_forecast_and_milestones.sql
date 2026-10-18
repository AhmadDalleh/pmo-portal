-- Report 4a: monthly posted spend next to monthly forecast, portfolio level.
-- Spend and forecast are each summed to one row per month in their own CTE, then joined on the month
-- (joining the two fact tables row by row would multiply amounts). The reporting date is in reporting_settings.
WITH spend AS (
    SELECT month_start, SUM(amount) AS actual_spend
    FROM vw_cost_facts
    GROUP BY month_start
),
forecast AS (
    SELECT period_month AS month_start, SUM(amount) AS forecast
    FROM vw_forecast_facts
    GROUP BY period_month
)
SELECT COALESCE(s.month_start, f.month_start) AS month_start,
       COALESCE(s.actual_spend, 0) AS actual_spend,
       COALESCE(f.forecast, 0) AS forecast,
       SUM(COALESCE(s.actual_spend, 0)) OVER (ORDER BY COALESCE(s.month_start, f.month_start)) AS cumulative_actual
FROM spend s
FULL JOIN forecast f ON f.month_start = s.month_start
ORDER BY 1;

-- Report 4b: milestone status per project (pending / done / overdue / due within 14 days).
SELECT project_code,
       COUNT(*) AS milestones,
       COUNT(*) FILTER (WHERE status = 'done') AS completed,
       COUNT(*) FILTER (WHERE is_overdue) AS overdue,
       COUNT(*) FILTER (WHERE is_due_soon) AS due_within_14_days
FROM vw_milestone_facts
GROUP BY project_code
ORDER BY overdue DESC, project_code;
