-- Report 2: project financial performance (database-design.md section 6.1).
-- current budget = initial + approved revisions effective on or before the reporting date (pending/rejected ignored)
-- actual spend   = posted costs on or before the reporting date (drafts and later-dated costs excluded; reversals net out)
-- EAC            = actual spend + forecast-to-complete (forecast months after the reporting month); projected variance = budget - EAC
-- The view aggregates revisions, costs and forecasts in separate CTEs before joining, so spend is never multiplied.
SELECT project_code,
       project_name,
       department,
       current_budget,
       actual_spend,
       remaining_budget,
       ROUND(utilization * 100, 1) AS utilization_pct,
       forecast_to_complete,
       eac,
       projected_variance,
       CASE WHEN is_overspent THEN 'Overspent'
            WHEN is_projected_overrun THEN 'Projected overrun'
            WHEN is_near_budget THEN 'Near budget (90-100%)'
            WHEN has_no_forecast THEN 'No forecast'
            ELSE 'On track' END AS finance_flag
FROM vw_project_financials
ORDER BY projected_variance ASC, project_code;
