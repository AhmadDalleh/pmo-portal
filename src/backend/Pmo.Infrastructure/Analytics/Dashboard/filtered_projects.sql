-- THE shared filter of every dashboard KPI, chart and drill-down (stories G1-G4).
-- A project is "in the dashboard" when it is in the caller's scope and matches the filters below.
-- Because every query starts from this one SELECT, a drill-down always lists exactly the rows behind its KPI.
-- Parameters: @scope_ids int[], filters (NULL = no filter): @project_id, @department_id, @manager_id (project owner), @status,
-- @from / @to (keeps projects whose start..planned end overlaps the window).
SELECT p.id, p.code, p.name, p.status, p.department_id, p.owner_id
FROM projects p
WHERE p.id = ANY(@scope_ids)
  AND (@project_id::int IS NULL OR p.id = @project_id::int)
  AND (@department_id::int IS NULL OR p.department_id = @department_id::int)
  AND (@manager_id::int IS NULL OR p.owner_id = @manager_id::int)
  AND (@status::text IS NULL OR p.status = @status::text)
  AND (@from::date IS NULL OR p.planned_end_date >= @from::date)
  AND (@to::date IS NULL OR p.start_date <= @to::date)
