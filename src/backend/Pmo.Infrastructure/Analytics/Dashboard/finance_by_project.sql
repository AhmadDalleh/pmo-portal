/*FINANCE*/
SELECT fp.id AS project_id, fp.code, fp.name,
       pp.current_budget,
       pp.actual_spend,
       pp.forecast_to_complete,
       pp.actual_spend + pp.forecast_to_complete AS eac
FROM fp
JOIN per_project pp ON pp.project_id = fp.id
ORDER BY fp.code
