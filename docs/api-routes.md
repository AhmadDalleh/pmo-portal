# API routes

Base path `/api`. All routes need `Authorization: Bearer <accessToken>` unless marked **anon**. Errors are RFC 7807 `ProblemDetails`
with `code` (stable business-rule id), `errors` (field → messages, validation only) and `correlationId`.
Policies: **PM** = Project Manager or Administrator (role policy only; the service then checks the project scope), **ADM** = Administrator.
Out-of-scope projects return 404, visible-but-not-allowed returns 403.

## Health and auth (slice 3)
| Method | Route | Who | Success | Notes |
|---|---|---|---|---|
| GET | `/health` | anon | 200 | 503 when PostgreSQL is down |
| POST | `/auth/login` | anon | 200 | body `{email,password}`; sets HttpOnly refresh cookie; 401 generic on any failure; rate limited |
| POST | `/auth/refresh` | anon (cookie) | 200 | rotates the refresh cookie; 401 if missing, used, expired or revoked |
| POST | `/auth/logout` | anon (cookie) | 204 | revokes the refresh token |
| GET | `/auth/me` | any | 200 | profile + `managedProjectIds` |

## Projects and lookups (slice 3)
| Method | Route | Who | Success | Errors |
|---|---|---|---|---|
| GET | `/projects?search&departmentId&ownerId&status&from&to&sort&dir&page&pageSize&asOf` | any (scope) | 200 paged | 400 unknown sort/status/page |
| GET | `/projects/{id}` | any (scope) | 200 | 404 |
| GET | `/projects/{id}/summary?asOf` | any (scope) | 200 | 404 |
| POST | `/projects` | PM | 201 + Location | 400 `owner_required`/`owner_not_project_manager`/validation, 403, 409 `duplicate_project_code` |
| PUT | `/projects/{id}` | PM + manages project | 200 | 400, 403, 404, 409 `concurrency_conflict` |
| GET | `/projects/{id}/members` | any (scope) | 200 | 404 |
| POST | `/projects/{id}/members` | PM + manages project | 201 | 400 `manager_must_be_pm`/`invalid_member`, 403, 404, 409 `already_member` |
| DELETE | `/projects/{id}/members/{userId}` | PM + manages project | 204 | 409 `cannot_remove_owner` / `member_has_open_tasks` |
| GET | `/lookups/departments` · `/lookups/users?role=` · `/lookups/cost-categories` | any | 200 | |

Sort keys: `code, name, department, owner, status, start, end, progress, utilization`; `dir` = `asc|desc`; default page 1, size 20 (max 100).
