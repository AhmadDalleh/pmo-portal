-- Spend and forecast per cost category over the filtered projects. @category_id applies here (a finance visual).
, actual AS (
    SELECT ct.category_id, SUM(ct.amount) AS actual_spend
    FROM cost_transactions ct
    JOIN fp ON fp.id = ct.project_id
    WHERE ct.status = 'posted' AND ct.txn_date <= @as_of::date
      AND (@category_id::int IS NULL OR ct.category_id = @category_id::int)
    GROUP BY ct.category_id
),
ftc AS (
    SELECT f.category_id, SUM(f.amount) AS forecast_to_complete
    FROM financial_forecasts f
    JOIN fp ON fp.id = f.project_id
    WHERE f.period_month > date_trunc('month', @as_of::date)::date
      AND (@category_id::int IS NULL OR f.category_id = @category_id::int)
    GROUP BY f.category_id
)
SELECT c.id AS category_id, c.code, c.name,
       COALESCE(a.actual_spend, 0) AS actual_spend, COALESCE(f.forecast_to_complete, 0) AS forecast_to_complete
FROM cost_categories c
LEFT JOIN actual a ON a.category_id = c.id
LEFT JOIN ftc f ON f.category_id = c.id
WHERE a.category_id IS NOT NULL OR f.category_id IS NOT NULL
ORDER BY c.name
