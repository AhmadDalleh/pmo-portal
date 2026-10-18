-- Report 3: open high-exposure risks and issues.
-- Exposure score = probability x impact (stored as risks.score). High = 15 or more. Open = status open or mitigating,
-- so a closed high-exposure risk is excluded (it is in the data on purpose).
SELECT project_code,
       department,
       type,
       title,
       owner_name,
       probability,
       impact,
       score,
       status,
       due_date
FROM vw_risk_facts
WHERE is_open_high_exposure
ORDER BY score DESC, due_date, risk_id;
