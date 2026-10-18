# 06 — Risks and issues (D1–D3)

## 1. Business goal
The PMO needs an early warning list: what could go wrong (risks) and what has already gone wrong (issues), ranked by exposure.
High exposure (probability × impact ≥ 15) must come with a mitigation plan and must stand out until it is closed.
A closed risk must never inflate the "open high-exposure" numbers that management reads.

## 2. UI screens and actions
| Screen | Click | Result |
|---|---|---|
| Project → **Risks & issues** | Read the table | Worst first: type, title, owner, score "16 (4 x 4) - HIGH" (text, not only colour), status badge, due date. |
| Same | **New** / pencil (PM/Admin of the project) | Dialog: type, title, owner (members), probability 1–5, impact 1–5, status, due date, mitigation, description. Live line "Score 16 - high exposure: a mitigation plan is required." |
| Dashboard → **Open high-exposure risks** card | Click | Drill-down `/dashboard/drilldown/risks?highExposure=true`: the open risks behind the number. |
| Dashboard → **Risk matrix** | Click a cell | Drill-down for that probability/impact (D3). |
| Cross-project risk list (D2) | – | **API only**: `GET /api/risks?highExposure=true&status=open`. No screen. |

## 3. Per action
| Action | Route | Chain | Rules | Tables / transaction |
|---|---|---|---|---|
| Project list | `GET /api/projects/{id}/risks` | `RisksController` → `RiskService.ListForProjectAsync` | view scope (404) | reads `risks` |
| Cross-project | `GET /api/risks?highExposure&status(open|closed|all)&projectId&probability&impact&page&pageSize` | `RiskService.SearchAsync` | scope ids; default status = open or mitigating; `Risk.HighExposureThreshold = 15` | reads |
| Create | `POST /api/projects/{id}/risks` (policy `ManageProjects`) | `RiskService.CreateAsync` → `Risk.ApplyStatus` | manage scope; p, i in 1–5; **score ≥ 15 needs mitigation** (400 `errors.mitigation`); owner must be a project member (`owner_not_member`); closed ⇒ `closed_at` | **tx, two saves**: `risks` (score computed by PostgreSQL: generated column `probability*impact`) then `audit_events` `risk.created` |
| Update | `PUT /api/risks/{id}` | `UpdateAsync` | same + `version` (409); closing stamps `closed_at`, reopening clears it (CHECK needs both) | one `SaveChanges`: `risks` + audit `risk.updated` |
| Dashboard | `GET /api/dashboard/risks/matrix`, `…/drilldown/risks` | `DashboardService` → SQL (`risk_matrix.sql`, `drilldown_risks.sql`) | open = status in (open, mitigating); KPI uses score ≥ 15 | reads |

## 4. Test scenarios
| Type | User | Steps | Expected |
|---|---|---|---|
| Happy | `pm1` | PRJ-001 → Risks → New: probability 2, impact 3, owner any member | Saved; score 6 (computed by the DB), not high exposure. |
| Validation | `pm1` | New: probability 4, impact 4, **empty mitigation** | 400 `errors.mitigation` "A mitigation plan is required when probability x impact is 15 or more." (the dialog also shows it before sending). Probability 6 → 400 `errors.probability`. |
| Forbidden role | `con1` (member of PRJ-001) | Swagger `POST /api/projects/1/risks` | **403**. |
| Out of scope | `con1` | `GET /api/projects/2/risks` | 404. |
| Owner rule | `pm1` | Owner = a user who is not a member | 400 `owner_not_member`. |
| Concurrency | `pm1` | PUT twice with the same `version` | 409 `concurrency_conflict`. |
| Seed edge: open high exposure | `admin1` | `GET /api/risks?highExposure=true&pageSize=100` | **4** items, worst first: PRJ-009 "Chatbot accuracy below target" (25), PRJ-002 "Vendor monitoring agent not certified" (20, mitigating), PRJ-001 "Key developer leaves before go-live" (16), PRJ-008 "CRM vendor upgrade breaks connectors" (15). Dashboard KPI "Open high-exposure risks" = 4. |
| Seed edge: closed high risk | `admin1` | `…?highExposure=true&status=closed` | PRJ-006 "Funding freeze extends beyond Q4" (score 20, closed) appears **only** here; it is not in the KPI. |
| Close/reopen | `pm1` | Close PRJ-001's high risk, then check the dashboard (managerId = pm1) | KPI drops by one and the project's health loses that Amber reason; reopen → back; `closed_at` set then NULL. |
| Matrix | `admin1` | Dashboard → matrix cell (5,5) | 1 open item (PRJ-009); drill-down lists it. Sum of all cells = number of open + mitigating risks. |

## 5. SQL to verify
```sql
select id, project_id, title, probability, impact, score, status, closed_at from risks where score >= 15 order by score desc;
select count(*) from risks where status in ('open','mitigating') and score >= 15;     -- 4 in the fresh seed = dashboard card
select probability, impact, count(*) from risks where status in ('open','mitigating') group by 1,2 order by 1 desc, 2;   -- the matrix
-- generated column: you cannot write it
update risks set score = 1 where id = 1;    -- ERROR: column "score" can only be updated to DEFAULT
select action, data from audit_events where entity_type = 'Risk' order by id desc limit 3;
select conname, pg_get_constraintdef(oid) from pg_constraint where conrelid='risks'::regclass and contype='c';
```

## 6. Interview questions
1. **How is the score calculated and why there?** A PostgreSQL generated column (`probability * impact`, stored), so every reader (API, SQL, Power BI) sees the same number and it cannot drift.
2. **Where is "high exposure needs mitigation" enforced?** Validator (400 with a field error) and mirrored in the dialog; the threshold is the constant `Risk.HighExposureThreshold` in the domain, used by the validator and the filters.
3. **Why does a closed risk not count?** The KPI and matrix use `status IN ('open','mitigating')`; `ApplyStatus` stamps `closed_at`, and a CHECK ties `closed_at` to the closed status.
4. **Why are risks and issues one table?** Same attributes and lifecycle; a `type` column separates them, which keeps queries and the matrix simple.
5. **What is not built for risks?** A cross-project risk screen (the API exists) and a separate matrix page; the matrix is a dashboard visual with drill-down.
