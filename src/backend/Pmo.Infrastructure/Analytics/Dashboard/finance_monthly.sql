-- Monthly trend (database-design.md section 6.2): posted spend up to the as-of date, forecast for later months.
-- @category_id applies here (a finance visual).
, actual AS (
    SELECT date_trunc('month', ct.txn_date)::date AS month, SUM(ct.amount) AS actual_spend
    FROM cost_transactions ct
    JOIN fp ON fp.id = ct.project_id
    WHERE ct.status = 'posted' AND ct.txn_date <= @as_of::date
      AND (@category_id::int IS NULL OR ct.category_id = @category_id::int)
    GROUP BY 1
),
forecast AS (
    SELECT f.period_month AS month, SUM(f.amount) AS forecast
    FROM financial_forecasts f
    JOIN fp ON fp.id = f.project_id
    WHERE f.period_month > date_trunc('month', @as_of::date)::date
      AND (@category_id::int IS NULL OR f.category_id = @category_id::int)
    GROUP BY 1
)
SELECT COALESCE(a.month, f.month) AS month, COALESCE(a.actual_spend, 0) AS actual_spend, COALESCE(f.forecast, 0) AS forecast
FROM actual a
FULL OUTER JOIN forecast f ON f.month = a.month
ORDER BY 1
