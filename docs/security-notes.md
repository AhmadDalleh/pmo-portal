# Security notes (K7)

| Topic | What is done | Where |
|---|---|---|
| Secrets | Connection string and `Jwt:Key` come from `dotnet user-secrets` locally and environment variables in Docker. `.env` is git-ignored; `.env.example` has placeholders. Demo password is never in the CSV files. | `.gitignore`, `.env.example`, `tools/DataGenerator/Import/DemoUserWriter.cs` |
| Passwords | ASP.NET Core Identity `PasswordHasher` (PBKDF2). Lockout after 5 failures for 5 minutes; inactive users cannot sign in; every attempt is audited (`auth.login_*`). Failures are always a generic 401. | `AddAuth`, `AuthService` |
| Access token | JWT HS256, 15 minutes, issuer/audience/lifetime/signature validated, key at least 32 characters (startup fails otherwise). Kept in browser memory only. | `AddAuth`, `JwtTokenService`, `auth.service.ts` |
| Refresh token | Random secret, only its SHA-256 hash stored (Identity `user_tokens`), rotated on every use, revoked on logout and on deactivation. Cookie: `HttpOnly; Secure; SameSite=Strict; Path=/api/auth`. | `RefreshTokenService`, `AuthController` |
| CORS | Allow-list from config (`Cors:AllowedOrigins`, default `http://localhost:4200`), credentials allowed for those origins only. | `Program.cs` |
| Rate limiting | Login: fixed window per IP (`RateLimiting:LoginPerMinute`, default 20), on top of account lockout. | `Program.cs` |
| Authorization | Two layers on every route: role policy, then project scope in the service. Out of scope 404 (existence not revealed), forbidden 403. Fallback policy requires sign-in; only `/api/health` and the auth routes are anonymous. | `Authorization/`, `ProjectScope` |
| Injection | EF parameters; Dapper parameters only. The sole text spliced into SQL is the ORDER BY taken from a fixed allow-list map and our own embedded `.sql` files. | `ProjectReadModel`, `DashboardReadModel` |
| Over-posting | Explicit command records per endpoint; ids and status fields that must not be client-controlled are not in them. | `Features/*/Commands` |
| Integrity | CHECK constraints, UNIQUE keys, append-only triggers (audit, history), posted-cost immutability trigger, optimistic concurrency (`xmin`). | migrations |
| Audit | Sensitive mutations write `audit_events` (actor, entity, action, correlation id, data without secrets). | services |
| Errors | RFC 7807 with a correlation id; unknown errors return a generic 500 and are logged with the id; no stack traces to clients. | `GlobalExceptionHandler` |
| Logging | Serilog, structured; no tokens or passwords logged. | `Program.cs` |

Known limits (not done): no HTTPS redirect or HSTS in the dev setup; access tokens stay valid up to 15 minutes after a role change or deactivation; no per-device session list; no antiforgery token because the API uses bearer tokens and the only cookie is path-limited `SameSite=Strict`; HS256 shared secret rather than asymmetric keys.
