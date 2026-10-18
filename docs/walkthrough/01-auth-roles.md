# 01 — Authentication, roles and scope (A1–A6)

Conventions for all walkthrough files: password of every demo user = your `DEMO_USER_PASSWORD`. Re-import the seed first (`./scripts/import-data.sh`) so numbers match. psql: `docker exec -it pmo-db psql -U pmo -d pmo_db`. "Swagger" = `http://localhost:5217/swagger` (Authorize button, paste `accessToken`). UI = `http://localhost:4200`.

## 1. Business goal
The portal holds budgets and approvals, so the PMO must know who is acting and limit what each person can see and do.
Three roles (Administrator, Project Manager, Contributor) plus project membership decide visibility ("scope") and actions.
Every sign-in and sensitive change is audited, so the PMO can answer "who did that and when".

## 2. UI screens and actions
| Screen | What to click | Result |
|---|---|---|
| `/login` | Enter `pm1@pmo.example` + password → Sign in | Lands on Projects; toolbar shows the name (Chanelle Stamm). |
| Same | Wrong password | Red message "Invalid email or password." (same text for unknown user, locked, inactive). |
| Toolbar menu | Look at the links per role | Admin/PM: Dashboard, Projects, My work, **Approvals**; Contributor: no Approvals. |
| Bell icon | Click | `/notifications` (module 05). |
| User menu (name) | Sign out | Cookie revoked, back to `/login`. |
| Browser reload on any page | F5 | Still signed in: the app calls `/api/auth/refresh` at startup using the HttpOnly cookie. |
| Admin screens | – | **None in the UI.** Use Swagger for `/api/admin/*`. |

## 3. Per action: route → controller → service → rule → tables
| Action | Route | Chain | Tables / commit |
|---|---|---|---|
| Sign in | `POST /api/auth/login` `{email,password}` (anonymous, rate limited) | `AuthController.Login` → `AuthService.LoginAsync` → Identity `UserManager` (find, lock check, `CheckPasswordAsync`) → `JwtTokenService` + `RefreshTokenService` | `users` (access_failed_count / lockout_end), `audit_events` (`auth.login_succeeded` / `auth.login_failed`), `user_tokens` (refresh hash). **Not one transaction**: failure counter, audit row and token row are separate commits. |
| Refresh | `POST /api/auth/refresh` (cookie) | `AuthService.RefreshAsync` → `RefreshTokenService.ConsumeAsync` (delete used row) then `IssueAsync` (insert new) | `user_tokens`; two commits (old row deleted, then new inserted). |
| Sign out | `POST /api/auth/logout` | `RefreshTokenService.RevokeAsync` | deletes the `user_tokens` row; cookie cleared. |
| Who am I | `GET /api/auth/me` | `AuthController.Me` → `IUserDirectory` + `ProjectAccess.ManagedIdsAsync` | reads `users`, `user_roles`, `roles`, `project_members`. |
| Role gate | `[Authorize(Policy=…)]` (`ManageProjects`, `CanDecide`, `CanPostCosts`, `AdminOnly`) | `AuthorizationExtensions` | none |
| Scope | every project route | `IProjectScope` (`ProjectScope`): view = admin or member; manage = admin, or PM who is a **manager member** | `project_members` |
| List/role/status of users | `GET /api/admin/users`, `PUT …/{id}/role`, `PUT …/{id}/status` (ADM) | `AdminController` → `UserAdminService` | role change: `user_roles`, `project_members` (manager → contributor on demotion), `audit_events` in **one transaction**; deactivate: `users.is_active`, deletes `user_tokens`, audit, one transaction. |
| Audit review | `GET /api/admin/audit-events?…` (ADM) | `AdminService.AuditAsync` | reads `audit_events` |
| Notifications | `GET /api/notifications`, `…/unread-count`, `POST …/{id}/read` | `NotificationService` (own rows only; other's id → 404) | `notifications` |

Rules and where they live: lockout 5 tries / 5 min (`AddAuth` Identity options); inactive cannot sign in (`AuthService`); JWT 15 min with claims `sub,email,name,role` (`JwtTokenService`); cookie flags `HttpOnly; Secure; SameSite=Strict; Path=/api/auth` (`AuthController.CookieOptions`); out-of-scope 404 / forbidden 403 (`ProjectScope`, `GlobalExceptionHandler`).

## 4. Test scenarios
| Type | User | Steps | Expected |
|---|---|---|---|
| Happy | `pm1` | Sign in in the UI | `/projects` lists exactly PRJ-001, PRJ-006, PRJ-009 (3 rows). Swagger `/api/auth/me` → `role: "ProjectManager"`, `managedProjectIds: [1,6,9]`. |
| Validation | anonymous | Swagger `POST /api/auth/login` with empty email | 400 ProblemDetails, detail "Email and password are required." |
| Wrong password | `pm2` | Login with a wrong password | 401, `code: invalid_credentials`, detail "Invalid email or password." Same text for `nobody@pmo.example`. |
| Forbidden role | `pm1` | Swagger `GET /api/admin/users` | **403**. As `admin1`: 200 with 18 users. |
| Contributor menu | `con1` | Sign in | No Approvals link; `/api/auth/me` → `managedProjectIds: []`. |
| Edge: scope | `con1` | `GET /api/projects/2` (PRJ-002, not a member) | **404** `not_found`, not 403. |
| Edge: lockout | `con10` | 5 wrong passwords, then the **correct** one | 6th call still 401. SQL below shows `lockout_end` ~5 minutes ahead. Reset with the UPDATE in §5. |
| Edge: inactive | `admin1` then `con9` | Swagger `PUT /api/admin/users/16/status` `{"isActive":false}`; log in as `con9` | 401. Set back to true afterwards. |
| Edge: own role | `admin1` | `PUT /api/admin/users/1/role` `{"role":"Contributor"}` | 409 `cannot_change_own_role`. |
| Edge: PM owns projects | `admin1` | `PUT /api/admin/users/3/role` `{"role":"Contributor"}` (pm1) | 409 `user_owns_projects`. |
| Refresh rotation | any | Swagger `POST /api/auth/refresh` twice with the same cookie value | 1st 200, 2nd 401 (used token). |

## 5. SQL to verify
```sql
-- audit of sign-ins (newest first)
select occurred_at, action, actor_id, data from audit_events where action like 'auth.%' order by id desc limit 10;
-- lockout state
select email, access_failed_count, lockout_end, is_active from users where email in ('con10@pmo.example','con9@pmo.example');
-- reset after the lockout test
update users set lockout_end = null, access_failed_count = 0 where email = 'con10@pmo.example';
-- refresh tokens (only a hash is stored)
select user_id, name, left(value, 12) as hash_prefix from user_tokens order by user_id;
-- one role per user
select u.email, r.name from users u join user_roles ur on ur.user_id = u.id join roles r on r.id = ur.role_id order by u.id;
-- who is manager of what
select p.code, u.email, m.member_role from project_members m join projects p on p.id = m.project_id join users u on u.id = m.user_id where m.member_role = 'manager' order by p.code;
```

## 6. Interview questions
1. **Why two authorization layers?** The policy checks the role cheaply on the route; the service checks the project (scope) because the role alone does not say which project. The UI only hides buttons.
2. **Why 404 instead of 403 for projects outside scope?** 404 does not reveal that the id exists; 403 is used once the user may already see the project but not perform the action.
3. **How do refresh tokens work and what if one leaks?** Random secret in an HttpOnly, SameSite=Strict, path-limited cookie; only its SHA-256 hash is stored; rotated on every use, so a stolen copy works once; deleted on logout or deactivation.
4. **Why is the access token only in memory?** `localStorage` is readable by any injected script; memory is gone on reload and the cookie restores it.
5. **How are brute-force attacks handled?** Identity lockout (5 failures, 5 minutes), a per-IP rate limit on login, and generic error text; failures are audited.
