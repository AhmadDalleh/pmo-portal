# Backend patterns (.NET 10, EF Core + Npgsql, Dapper)

Use these shapes so every slice looks the same. Adapt names; don't add abstraction layers on top.

## Contents
1. Domain entity with transition rule
2. Domain exceptions → ProblemDetails
3. Current user & project scope
4. Application service with transaction + history + audit + outbox
5. Optimistic concurrency
6. Controller
7. Authorization policies
8. Idempotent create
9. Analytics query (Dapper, no fan-out)
10. Outbox processor
11. Correlation id middleware
12. Integration test (RBAC + persistence)

---

## 1. Domain entity with transition rule

```csharp
public sealed class ChangeRequest
{
    private static readonly Dictionary<CrStatus, CrStatus[]> Allowed = new()
    {
        [CrStatus.Draft]     = [CrStatus.Submitted, CrStatus.Withdrawn],
        [CrStatus.Submitted] = [CrStatus.Approved, CrStatus.Rejected, CrStatus.Returned],
        [CrStatus.Returned]  = [CrStatus.Submitted, CrStatus.Withdrawn],
    };

    public int Id { get; private set; }
    public int ProjectId { get; private set; }
    public int RequestedById { get; private set; }
    public CrStatus Status { get; private set; }
    public int? DecidedById { get; private set; }
    public DateTimeOffset? DecidedAt { get; private set; }
    public string? DecisionReason { get; private set; }
    public uint Version { get; private set; }               // mapped to xmin

    public ChangeRequestHistory Decide(CrStatus decision, int actorId, string reason, DateTimeOffset now)
    {
        if (decision is not (CrStatus.Approved or CrStatus.Rejected or CrStatus.Returned))
            throw new DomainRuleException("invalid_decision", "Decision must be approve, reject or return.");
        if (actorId == RequestedById)
            throw new DomainRuleException("self_approval_forbidden", "You cannot decide your own change request.", isForbidden: true);
        if (string.IsNullOrWhiteSpace(reason))
            throw new DomainRuleException("reason_required", "A decision reason is required.", isValidation: true);
        EnsureTransition(decision);

        var from = Status;
        Status = decision;
        DecidedById = actorId;
        DecidedAt = now;
        DecisionReason = reason.Trim();
        return ChangeRequestHistory.Create(Id, from, decision, actorId, DecisionReason, now);
    }

    private void EnsureTransition(CrStatus to)
    {
        if (!Allowed.TryGetValue(Status, out var targets) || !targets.Contains(to))
            throw new DomainRuleException("invalid_transition", $"Cannot move from {Status} to {to}.");
    }
}
```

Why on the entity: the rule is testable without a database and impossible to bypass from another service.

## 2. Domain exceptions → ProblemDetails

```csharp
public sealed class DomainRuleException(string code, string message,
    bool isForbidden = false, bool isValidation = false) : Exception(message)
{
    public string Code { get; } = code;
    public bool IsForbidden { get; } = isForbidden;
    public bool IsValidation { get; } = isValidation;
}

public sealed class PmoExceptionHandler(ILogger<PmoExceptionHandler> logger,
    IProblemDetailsService problems) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext ctx, Exception ex, CancellationToken ct)
    {
        var (status, code, title) = ex switch
        {
            ValidationException          => (400, "validation_failed", "Validation failed"),
            NotFoundException            => (404, "not_found", "Not found"),
            ForbiddenException           => (403, "forbidden", "Forbidden"),
            DomainRuleException { IsForbidden: true } d  => (403, d.Code, "Forbidden"),
            DomainRuleException { IsValidation: true } d => (400, d.Code, "Invalid request"),
            DomainRuleException d        => (409, d.Code, "Business rule violated"),
            DbUpdateConcurrencyException => (409, "concurrency_conflict", "Record was changed by someone else"),
            _                            => (500, "server_error", "Unexpected error")
        };
        if (status == 500) logger.LogError(ex, "Unhandled exception");
        else logger.LogWarning("Request failed {Status} {Code}: {Message}", status, code, ex.Message);

        ctx.Response.StatusCode = status;
        return await problems.TryWriteAsync(new()
        {
            HttpContext = ctx,
            ProblemDetails =
            {
                Status = status, Title = title,
                Detail = status == 500 ? "An unexpected error occurred." : ex.Message,
                Extensions =
                {
                    ["code"] = code,
                    ["correlationId"] = ctx.TraceIdentifier,
                    // FluentValidation errors → ["errors"] = dictionary of field → messages
                }
            }
        });
    }
}
```

## 3. Current user & project scope

```csharp
public interface ICurrentUser { int Id { get; } bool IsAdmin { get; } bool IsInRole(string role); }

public interface IProjectScope
{
    Task<int[]> AccessibleProjectIdsAsync(CancellationToken ct);  // admin → all
    Task EnsureCanViewAsync(int projectId, CancellationToken ct);  // else NotFoundException
    Task EnsureCanManageAsync(int projectId, CancellationToken ct);// view check, then ForbiddenException
}

// Implementation (Infrastructure)
public async Task EnsureCanManageAsync(int projectId, CancellationToken ct)
{
    await EnsureCanViewAsync(projectId, ct);
    if (user.IsAdmin) return;
    var isManager = await db.ProjectMembers.AnyAsync(m =>
        m.ProjectId == projectId && m.UserId == user.Id && m.MemberRole == MemberRole.Manager, ct);
    if (!isManager || !user.IsInRole(Roles.ProjectManager))
        throw new ForbiddenException("You cannot manage this project.");
}
```

404 for out-of-scope hides existence; 403 for in-scope-but-not-allowed is honest. Document this choice.

## 4. Application service with transaction + history + audit + outbox

```csharp
public sealed class ChangeRequestService(PmoDbContext db, ICurrentUser user,
    IProjectScope scope, TimeProvider clock, ICorrelationContext corr)
{
    public async Task<ChangeRequestDto> DecideAsync(int id, DecideChangeRequest cmd, CancellationToken ct)
    {
        var cr = await db.ChangeRequests.FirstOrDefaultAsync(x => x.Id == id, ct)
                 ?? throw new NotFoundException();
        await scope.EnsureCanManageAsync(cr.ProjectId, ct);
        db.Entry(cr).Property(x => x.Version).OriginalValue = cmd.Version;   // optimistic check

        var now = clock.GetUtcNow();
        await using var tx = await db.Database.BeginTransactionAsync(ct);

        var history = cr.Decide(cmd.Decision, user.Id, cmd.Reason, now);
        db.ChangeRequestHistory.Add(history with { CorrelationId = corr.Id });
        db.AuditEvents.Add(AuditEvent.For(cr, "change_request.decided", user.Id, cr.ProjectId, corr.Id,
            new { cmd.Decision, cmd.Reason }));
        if (cmd.Decision == CrStatus.Approved)
            db.OutboxMessages.Add(OutboxMessage.Create("ChangeRequestApproved",
                dedupKey: $"cr-approved:{cr.Id}", payload: new { ChangeRequestId = cr.Id }, now));

        await db.SaveChangesAsync(ct);   // one round trip; any failure throws before commit
        await tx.CommitAsync(ct);        // dispose without commit = rollback
        return ChangeRequestDto.From(cr);
    }
}
```

Failure story for the interview: if the history insert or outbox insert fails, `SaveChangesAsync` throws, the transaction is disposed without commit, PostgreSQL rolls back, and the CR status is unchanged. A concurrent second decision fails the `xmin` check → 409.

## 5. Optimistic concurrency

```csharp
// Configuration
builder.Property(x => x.Version).IsRowVersion();   // Npgsql maps uint + IsRowVersion to xmin
```
Update requests carry `Version`; assign it to `OriginalValue` before saving (as above). Response DTOs always return the new `Version`.

## 6. Controller

```csharp
[ApiController]
[Route("api/approvals/change-requests")]
[Authorize(Policy = Policies.CanDecide)]          // PM or Admin
public sealed class ApprovalsController(ChangeRequestService service) : ControllerBase
{
    [HttpPost("{id:int}/decision")]
    [ProducesResponseType<ChangeRequestDto>(200)]
    [ProducesResponseType<ProblemDetails>(400)]
    [ProducesResponseType<ProblemDetails>(403)]
    [ProducesResponseType<ProblemDetails>(404)]
    [ProducesResponseType<ProblemDetails>(409)]
    public async Task<ActionResult<ChangeRequestDto>> Decide(int id, DecideChangeRequest body, CancellationToken ct)
        => Ok(await service.DecideAsync(id, body, ct));
}
```

## 7. Authorization policies

```csharp
services.AddAuthorizationBuilder()
    .AddPolicy(Policies.CanDecide,      p => p.RequireRole(Roles.ProjectManager, Roles.Administrator))
    .AddPolicy(Policies.CanPostCosts,   p => p.RequireRole(Roles.ProjectManager, Roles.Administrator))
    .AddPolicy(Policies.AdminOnly,      p => p.RequireRole(Roles.Administrator))
    .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());
```
Role policy is layer 1; `IProjectScope` in the service is layer 2. Both are required.

## 8. Idempotent create

```csharp
public async Task<(CostDto Dto, bool Created)> CreateDraftAsync(int projectId, Guid idemKey,
    CreateCost cmd, CancellationToken ct)
{
    await scope.EnsureCanManageAsync(projectId, ct);
    var existing = await db.CostTransactions.AsNoTracking()
        .FirstOrDefaultAsync(c => c.IdempotencyKey == idemKey, ct);
    if (existing is not null) return (CostDto.From(existing), false);   // 200, not a duplicate
    // ... create, SaveChanges; a race on the same key hits the UNIQUE index → catch 23505 → re-read
}
```

## 9. Analytics query (Dapper, no fan-out)

```csharp
public sealed class FinanceAnalytics(NpgsqlDataSource ds)
{
    public async Task<IReadOnlyList<ProjectFinanceRow>> ByProjectAsync(DashboardFilter f, int[] scopeIds, CancellationToken ct)
    {
        const string sql = SqlResources.ProjectFinancials;   // the §6.1 query, embedded .sql file
        await using var conn = await ds.OpenConnectionAsync(ct);
        var rows = await conn.QueryAsync<ProjectFinanceRow>(new CommandDefinition(sql, new
        {
            as_of = f.AsOf, project_ids = scopeIds,
            department_id = f.DepartmentId, manager_id = f.ManagerId, status = f.Status
        }, cancellationToken: ct));
        return rows.AsList();
    }
}
```
Filters are applied as `(@department_id IS NULL OR p.department_id = @department_id)` — parameters only, never string concatenation. The drill-down query shares the same filter fragment.

## 10. Outbox processor

```csharp
public sealed class OutboxProcessor(IServiceScopeFactory scopes, ILogger<OutboxProcessor> log) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stop)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(10));
        while (await timer.WaitForNextTickAsync(stop))
        {
            using var scope = scopes.CreateScope();
            var runner = scope.ServiceProvider.GetRequiredService<OutboxBatchRunner>();
            try { await runner.RunOnceAsync(stop); }
            catch (Exception ex) { log.LogError(ex, "Outbox batch failed"); }
        }
    }
}
// RunOnceAsync: insert automation_runs(running) → in a transaction
//   SELECT ... FROM outbox_messages WHERE status='pending' AND available_at <= now()
//   ORDER BY id LIMIT 20 FOR UPDATE SKIP LOCKED
// for each: INSERT notifications ... ON CONFLICT (source_outbox_id, user_id) DO NOTHING;
//           mark processed | on error attempts+1, available_at = now() + 2^attempts min, failed after 5
// → commit → update automation_runs(succeeded|partial|failed, counts)
```

## 11. Correlation id middleware

```csharp
app.Use(async (ctx, next) =>
{
    var id = ctx.Request.Headers["X-Correlation-Id"].FirstOrDefault() ?? Guid.NewGuid().ToString("n");
    ctx.TraceIdentifier = id;
    ctx.Response.Headers["X-Correlation-Id"] = id;
    using (LogContext.PushProperty("CorrelationId", id)) await next();
});
```

## 12. Integration test (RBAC + persistence)

```csharp
public sealed class CostPostingTests(PmoApiFactory factory) : IClassFixture<PmoApiFactory>
{
    [Fact]
    public async Task PostCost_AsContributor_Returns403_AndCostStaysDraft()
    {
        var draftId = await factory.SeedDraftCostAsync(projectCode: "PRJ-007");
        var client = await factory.LoginAsync(DemoUsers.Contributor);   // member of PRJ-007

        var res = await client.PostAsJsonAsync($"/api/finance/costs/{draftId}/post", new { version = 0u });

        res.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await factory.QueryScalarAsync<string>(
            "select status from cost_transactions where id = @id", new { id = draftId }))
            .ShouldBe("draft");
    }
}
```
`PmoApiFactory` = `WebApplicationFactory<Program>` + Testcontainers PostgreSQL, applies migrations and a small deterministic fixture (not the full CSV seed) per test class.
