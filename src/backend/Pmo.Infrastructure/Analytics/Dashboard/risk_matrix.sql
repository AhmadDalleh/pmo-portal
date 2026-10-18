-- 5 x 5 matrix of open (open or mitigating) risks and issues; empty cells are returned with 0.
, cells AS (
    SELECT p AS probability, i AS impact FROM generate_series(1, 5) p CROSS JOIN generate_series(1, 5) i
),
open_items AS (
    SELECT r.probability, r.impact, COUNT(*) AS open_count
    FROM risks r
    JOIN fp ON fp.id = r.project_id
    WHERE r.status IN ('open', 'mitigating')
    GROUP BY r.probability, r.impact
)
SELECT c.probability, c.impact, COALESCE(o.open_count, 0)::int AS open_count
FROM cells c
LEFT JOIN open_items o ON o.probability = c.probability AND o.impact = c.impact
ORDER BY c.probability DESC, c.impact
