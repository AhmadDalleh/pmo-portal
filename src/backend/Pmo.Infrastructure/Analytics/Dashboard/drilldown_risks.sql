-- Open risks and issues behind the risk KPI and the matrix cells (probability / impact optional).
SELECT r.id, fp.code AS project_code, r.type, r.title, u.full_name AS owner_name, r.probability::int AS probability, r.impact::int AS impact,
       r.score::int AS score, r.status, r.due_date, COUNT(*) OVER ()::int AS total_count
FROM risks r
JOIN fp ON fp.id = r.project_id
JOIN users u ON u.id = r.owner_id
WHERE r.status IN ('open', 'mitigating')
  AND (@probability::int IS NULL OR r.probability = @probability::int)
  AND (@impact::int IS NULL OR r.impact = @impact::int)
  AND (@high_exposure::boolean IS NOT TRUE OR r.score >= 15)
ORDER BY r.score DESC, r.id
LIMIT @page_size OFFSET @offset
