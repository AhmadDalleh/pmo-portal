# PMO web (Angular 22)

Browser client of the PMO Portfolio & Financial Control Portal. It holds no business data or KPI maths: every number comes from the API and is re-read after each change. Overall design: `docs/architecture.md`.

## Run
```bash
npm install
npm start          # http://localhost:4200, proxies /api to http://localhost:5217 (proxy.conf.json)
npm run build      # production bundle in dist/pmo-web
```
Start the API first (`dotnet run --project src/backend/Pmo.Api`, see the root README). Demo logins are in the root README.

## Structure (`src/app`)
| Folder | What lives there |
|---|---|
| `core/auth` | `AuthService` (access token kept in memory in a signal, session restored at startup from the HttpOnly refresh cookie), `authGuard`, `managerGuard`, `adminGuard` |
| `core/http` | Functional interceptors: correlation id header, bearer token with one refresh-and-retry on 401, error mapping to `ApiError` (RFC 7807 fields) |
| `core/api` | One service per backend resource (`projects.api.ts`, `tasks.api.ts`, `finance.api.ts`, `governance.api.ts`, `dashboard.api.ts`, `admin.api.ts`); the only place that builds URLs |
| `core/models` | TypeScript types that mirror the API DTOs |
| `shared` | Reusable pieces: `data-table` (sort, server paging, empty/loading/error, expandable detail row), `async-state`, `confirm-dialog`, `status-badge` (icon and text, not colour only), `kpi-card`, `chart-card`, `notify` (toasts and server field errors) |
| `features/<area>` | Pages and dialogs: `auth`, `projects`, `tasks`, `finance`, `risks`, `change-requests`, `notifications`, `dashboard`, `admin` |
| `app.routes.ts` | Lazy-loaded routes; guards only improve navigation, the server checks every call again |

## Conventions
- Standalone components, signals for state; a small store service per feature where several widgets share state (`dashboard-store.ts`).
- After any create, update or transition the component reloads from the API; nothing patches totals locally.
- Forms are reactive; client validation mirrors the server rules and server `errors` are placed under the matching field (`applyServerErrors`).
- Consequential actions (post or reverse a cost, decide a change request, role change, retry) use `confirm-dialog`.
- Dashboard filters live in the URL query string, so a view can be shared and survives reload.
- Charts: ngx-echarts with tree-shaken registration (`core/charts.ts`); chart options are built from API data in `features/dashboard/chart-options.ts`.

## Tests
`npm test` runs Vitest (jsdom) through `ng test`. The specs guard against reload loops: `project-shell.spec.ts` (one `GET /api/projects/{id}` per route id) and `project-costs.spec.ts` (one request per page change). Rule for every `effect()` that loads data: track only the input signal and call `load()` inside `untracked()`, otherwise a signal that `load()` reads and later writes re-triggers the effect. Everything else is covered by the API integration tests (`tests/Pmo.IntegrationTests`) and the manual checklist `docs/manual-test-checklist.md`.
