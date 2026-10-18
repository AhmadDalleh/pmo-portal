# 02 — Projects, members and scope (B1–B6)

## 1. Business goal
The portfolio list is where the PMO sees every project, its owner, progress, budget usage and a health badge.
Creating a project must create its budget, owner and membership together, otherwise cost tracking and access break.
Membership is what grants access, so adding and removing people is a controlled, audited action.

## 2. UI screens and actions
| Screen | Click | Result |
|---|---|---|
| Projects (`/projects`) | Type in **Search**, pick **Department**/**Status**, click column headers, change page size | Server-side search, filter, sort, paging. Columns: code, name, department, owner, status, progress, budget used, health badge (icon + text). |
| Projects | **New project** (PM/Admin only) | `/projects/new` form: code, name, department, owner (Admin only), dates, initial budget, description. |
| New project form | Submit with end date before start date | Message under *Planned end date*. |
| New project form | Submit valid data | Toast "Project … created", opens the project. |
| Click a row | – | Project page with header and tabs: Overview, Tasks, Milestones, Finance, Risks & issues, Change requests, Members. |
| Overview | Read cards; **Edit project** (managers) | Delivery, governance, finance cards (each has an info tooltip with the formula), progress-history chart. Edit dialog: name, status, owner, dates, actual end, description. |
| Members tab | **Add person** + role → Add; remove icon (managers) | Adds/removes membership; removal asks for confirmation. |

## 3. Per action
| Action | Route | Chain | Rule(s) | Tables / transaction |
|---|---|---|---|---|
| List/search | `GET /api/projects?search&departmentId&ownerId&status&from&to&sort&dir&page&pageSize&asOf` | `ProjectsController.List` → `ProjectService.ListAsync` (validator, scope ids) → `ProjectReadModel` + `ProjectPortfolio.sql` | sort key allow-list (400 otherwise); health via `ProjectHealth` | read `projects, tasks, risks, change_requests, project_budgets, budget_revisions, cost_transactions, financial_forecasts, users, departments` |
| Details / summary | `GET /api/projects/{id}`, `GET …/{id}/summary?asOf` | `ProjectService.GetAsync / GetSummaryAsync` | `IProjectScope.EnsureCanViewAsync` → 404 | reads |
| Create | `POST /api/projects` (policy `ManageProjects`) | `ProjectService.CreateAsync` | PM becomes owner (naming another owner → 403); Admin must name a PM (`owner_required`); owner must be an active PM; unique code (409); validator dates/budget | **Explicit transaction, two saves**: (1) `projects`; (2) `project_budgets` + `project_members` (manager) + `audit_events` `project.created`; commit. A failure in step 2 rolls the project back too (tested with a trigger). |
| Edit | `PUT /api/projects/{id}` | `ProjectService.UpdateAsync` | `EnsureCanManage`; `completed` needs `actualEndDate`; `Version`→`xmin` (409); owner change: new owner → manager, old owner → contributor | one `SaveChanges`: `projects`, `project_members`, `audit_events` |
| List members | `GET /api/projects/{id}/members` | `ProjectMemberService.ListAsync` | view scope | reads |
| Add member | `POST /api/projects/{id}/members` `{userId,memberRole}` | `ProjectMemberService.AddAsync` | manage scope; user active, not Administrator; manager role only for PMs; duplicate → 409 `already_member` | one `SaveChanges`: `project_members` + `audit_events` `project.member_added` |
| Remove member | `DELETE …/members/{userId}` | `ProjectMemberService.RemoveAsync` | cannot remove owner (409); open tasks assigned → 409 listing them | one `SaveChanges`: delete + `audit_events` |
| Lookups | `GET /api/lookups/departments|users|cost-categories` | `LookupService` | any signed-in user | reads |

## 4. Test scenarios
| Type | User | Steps | Expected |
|---|---|---|---|
| Happy | `pm2` | New project code `PRJ-011`, dept Digital Channels, dates 2026-10-01 → 2027-03-31, budget 250000 | 201; code stored uppercase; status `proposed`; owner = Eladio Altenwerth (pm2) as manager member; appears in list. |
| Validation | `pm2` | End date 2026-09-01 (before start), budget -1 | 400, `errors.plannedEndDate` "Planned end date must be on or after the start date." and `errors.initialBudget`. UI shows them under the fields. |
| Duplicate | `pm2` | Create `prj-011` again | 409 `duplicate_project_code`. |
| Forbidden | `con1` | `POST /api/projects` in Swagger | **403** (policy). |
| Forbidden (in scope) | `pm4` (contributor member of PRJ-001) | `PUT /api/projects/1` | **403** (service), not 404. pm4 on `PUT /api/projects/2` → 404. |
| Out of scope | `con1` | `GET /api/projects/2` | 404. |
| Concurrency | `pm2` | Open the project, edit in two tabs; save tab 1, then tab 2 | Tab 2: 409 `concurrency_conflict`, toast with server text; page reloads current data. |
| Seed edge | `admin1` | Project list | PRJ-009 health **Off track (red)**, budget used 109.0%; PRJ-001 **At risk (amber)**, 92.0%; PRJ-010 progress **"No tasks"** (not 0%); PRJ-004 status Completed. |
| Seed edge: summary | `admin1` | PRJ-001 Overview | Budget AED 1,300,000, spend 1,196,000, remaining 104,000, EAC 1,286,000, variance 14,000, overdue tasks 1, next milestone 2026-10-10, high risks 1. |
| Member rule | `pm5` (owns PRJ-008) | Remove the owner's membership via Swagger `DELETE /api/projects/8/members/7` | 409 `cannot_remove_owner`. Remove a member who has an open task → 409 `member_has_open_tasks` listing it. |

## 5. SQL to verify
```sql
select id, code, name, status, owner_id, xmin::text as version from projects order by id desc limit 3;
select * from project_budgets where project_id = (select max(id) from projects);
select user_id, member_role from project_members where project_id = (select max(id) from projects);
select action, entity_id, data from audit_events where action like 'project.%' order by id desc limit 5;
-- scope of con1
select p.code from project_members m join projects p on p.id = m.project_id where m.user_id = (select id from users where email = 'con1@pmo.example') order by 1;  -- PRJ-001, 004, 007, 008
-- the list's KPI parts for one project (PRJ-001)
select (select sum(weight*progress_pct)::numeric/sum(weight) from tasks where project_id=1 and status<>'cancelled') progress,
       (select count(*) from tasks where project_id=1 and due_date < '2026-09-30' and status not in ('done','cancelled')) overdue;
```

## 6. Interview questions
1. **What is committed together on project creation?** Project, initial budget, owner membership and audit row; the first save is needed to get the generated id, so an explicit transaction wraps both saves.
2. **How does the health badge work and where is it?** `ProjectHealth.Evaluate` (Red: overspent, projected overrun or 3+ overdue; Amber: 90%+ used, any overdue or open high risk), applied to numbers returned by SQL.
3. **How do you avoid overwriting someone else's edit?** The update carries `version` (PostgreSQL `xmin`); EF adds it to the WHERE clause; zero rows updated raises a concurrency exception mapped to 409.
4. **How is sorting safe from SQL injection?** The API sort key is validated against an allow-list and mapped to a fixed column name; user text never reaches the SQL.
5. **Why does changing the owner touch memberships?** Manage rights come from being a manager member, so the new owner must become a manager and the old one is demoted to contributor in the same save.
