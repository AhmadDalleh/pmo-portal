-- The posted transactions behind "actual spend": same projects, same cutoff, same category filter as the charts.
-- total_amount is the sum of ALL matching rows (not just this page); it must equal the KPI or chart value.
SELECT ct.id, fp.code AS project_code, c.name AS category_name, ct.txn_date, ct.amount, ct.description, ct.vendor_name, ct.reference_no,
       COUNT(*) OVER ()::int AS total_count, SUM(ct.amount) OVER () AS total_amount
FROM cost_transactions ct
JOIN fp ON fp.id = ct.project_id
JOIN cost_categories c ON c.id = ct.category_id
WHERE ct.status = 'posted' AND ct.txn_date <= @as_of::date
  AND (@category_id::int IS NULL OR ct.category_id = @category_id::int)
ORDER BY ct.txn_date DESC, ct.id DESC
LIMIT @page_size OFFSET @offset
