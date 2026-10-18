-- Budget, actual spend and forecast-to-complete per filtered project (database-design.md section 6.1).
-- Each one-to-many table is aggregated in its own CTE and only then joined, so nothing is counted twice (section 7).
-- Included by the KPI query and by the budget-vs-actual-vs-EAC chart, so both use the same definitions.
, budget AS (
    SELECT pb.project_id,
           pb.initial_amount
         + COALESCE(SUM(br.amount) FILTER (WHERE br.status = 'approved' AND br.effective_date <= @as_of::date), 0) AS current_budget
    FROM project_budgets pb
    JOIN fp ON fp.id = pb.project_id
    LEFT JOIN budget_revisions br ON br.project_id = pb.project_id
    GROUP BY pb.project_id, pb.initial_amount
),
actual AS (
    SELECT ct.project_id, SUM(ct.amount) AS actual_spend
    FROM cost_transactions ct
    JOIN fp ON fp.id = ct.project_id
    WHERE ct.status = 'posted' AND ct.txn_date <= @as_of::date
    GROUP BY ct.project_id
),
ftc AS (
    SELECT f.project_id, SUM(f.amount) AS forecast_to_complete
    FROM financial_forecasts f
    JOIN fp ON fp.id = f.project_id
    WHERE f.period_month > date_trunc('month', @as_of::date)::date
    GROUP BY f.project_id
),
per_project AS (
    SELECT b.project_id, b.current_budget,
           COALESCE(a.actual_spend, 0) AS actual_spend,
           COALESCE(f.forecast_to_complete, 0) AS forecast_to_complete
    FROM budget b
    LEFT JOIN actual a ON a.project_id = b.project_id
    LEFT JOIN ftc f ON f.project_id = b.project_id
)
