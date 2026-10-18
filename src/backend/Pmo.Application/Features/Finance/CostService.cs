using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Pmo.Application.Common;
using Pmo.Application.Common.Abstractions;
using Pmo.Application.Common.Exceptions;
using Pmo.Application.Features.Finance.Commands;
using Pmo.Application.Features.Finance.Dtos;
using Pmo.Application.Features.Finance.Queries;
using Pmo.Application.Features.Projects;
using Pmo.Domain.Automation;
using Pmo.Domain.Common;
using Pmo.Domain.Finance;
using Pmo.Domain.Governance;

namespace Pmo.Application.Features.Finance;

/// <summary>Stories F5, F6, F7: draft costs, posting, reversals. Posted rows are never edited or deleted.</summary>
public sealed class CostService(
    IPmoDbContext db, ICurrentUser user, IProjectScope scope, CostMapper mapper, IProjectReadModel projects,
    IOptions<ReportingOptions> reporting, TimeProvider clock, ICorrelationIdAccessor correlation,
    IValidator<CreateCostCommand> createValidator, IValidator<UpdateCostCommand> updateValidator,
    IValidator<ReverseCostCommand> reverseValidator, IValidator<CostSearchQuery> searchValidator)
{
    public async Task<PagedResult<CostDto>> ListAsync(int projectId, CostSearchQuery query, CancellationToken ct)
    {
        await scope.EnsureCanViewAsync(projectId, ct);
        await searchValidator.ValidateAndThrowAsync(query, ct);

        var costs = db.CostTransactions.AsNoTracking().Where(c => c.ProjectId == projectId);
        if (query.Status is not null) costs = costs.Where(c => c.Status == query.Status);
        if (query.CategoryId is not null) costs = costs.Where(c => c.CategoryId == query.CategoryId);
        if (query.From is not null) costs = costs.Where(c => c.TxnDate >= query.From);
        if (query.To is not null) costs = costs.Where(c => c.TxnDate <= query.To);

        var total = await costs.CountAsync(ct);
        var page = await costs.OrderByDescending(c => c.TxnDate).ThenByDescending(c => c.Id)
            .Skip((query.Page - 1) * query.PageSize).Take(query.PageSize).ToListAsync(ct);
        return new PagedResult<CostDto>(await mapper.ToDtosAsync(page, ct), query.Page, query.PageSize, total);
    }

    public async Task<CostDto> GetAsync(long costId, CancellationToken ct)
    {
        var cost = await db.CostTransactions.AsNoTracking().FirstOrDefaultAsync(c => c.Id == costId, ct) ?? throw new NotFoundException("Cost", costId);
        await scope.EnsureCanViewAsync(cost.ProjectId, ct);
        return (await mapper.ToDtosAsync([cost], ct)).Single();
    }

    /// <summary>
    /// F5. The Idempotency-Key makes a repeated request return the original row (Created = false) instead of a duplicate;
    /// a UNIQUE index on the key is the safety net.
    /// </summary>
    public async Task<(CostDto Cost, bool Created)> CreateDraftAsync(int projectId, Guid? idempotencyKey, CreateCostCommand cmd, CancellationToken ct)
    {
        await scope.EnsureCanManageAsync(projectId, ct);
        if (idempotencyKey is null)
        {
            throw new DomainRuleException("idempotency_key_required", "Send an Idempotency-Key header (a new GUID per form).", isValidation: true);
        }

        var existing = await db.CostTransactions.AsNoTracking().FirstOrDefaultAsync(c => c.IdempotencyKey == idempotencyKey, ct);
        if (existing is not null)
        {
            if (existing.ProjectId != projectId)
            {
                throw new DomainRuleException("idempotency_key_reused", "This Idempotency-Key was already used for another project.");
            }

            return ((await mapper.ToDtosAsync([existing], ct)).Single(), false);
        }

        await createValidator.ValidateAndThrowAsync(cmd, ct);
        await EnsureActiveCategoryAsync(cmd.CategoryId, ct);

        var now = clock.GetUtcNow();
        var cost = new CostTransaction
        {
            ProjectId = projectId, CategoryId = cmd.CategoryId, TxnDate = cmd.TxnDate, Amount = cmd.Amount, Description = cmd.Description.Trim(),
            VendorName = cmd.VendorName?.Trim(), ReferenceNo = cmd.ReferenceNo?.Trim(), Status = CostStatus.Draft,
            IdempotencyKey = idempotencyKey.Value, CreatedById = user.Id, CreatedAt = now
        };

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        db.CostTransactions.Add(cost);
        await db.SaveChangesAsync(ct);
        db.AuditEvents.Add(AuditEvent.Create("cost.draft_created", "CostTransaction", cost.Id, user.Id, projectId, correlation.CorrelationId, now,
            new { cost.Amount, cost.CategoryId, cost.TxnDate }));
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return ((await mapper.ToDtosAsync([cost], ct)).Single(), true);
    }

    public async Task<CostDto> UpdateDraftAsync(long costId, UpdateCostCommand cmd, CancellationToken ct)
    {
        var cost = await LoadForManageAsync(costId, ct);
        cost.EnsureDraft();   // posted => 409 posted_immutable
        await updateValidator.ValidateAndThrowAsync(cmd, ct);
        await EnsureActiveCategoryAsync(cmd.CategoryId, ct);
        db.Entry(cost).Property(c => c.Version).OriginalValue = cmd.Version;

        var before = new { cost.Amount, cost.CategoryId, cost.TxnDate };
        cost.CategoryId = cmd.CategoryId;
        cost.TxnDate = cmd.TxnDate;
        cost.Amount = cmd.Amount;
        cost.Description = cmd.Description.Trim();
        cost.VendorName = cmd.VendorName?.Trim();
        cost.ReferenceNo = cmd.ReferenceNo?.Trim();

        db.AuditEvents.Add(AuditEvent.Create("cost.draft_updated", "CostTransaction", cost.Id, user.Id, cost.ProjectId, correlation.CorrelationId,
            clock.GetUtcNow(), new { before, after = new { cost.Amount, cost.CategoryId, cost.TxnDate } }));
        await db.SaveChangesAsync(ct);
        return (await mapper.ToDtosAsync([cost], ct)).Single();
    }

    public async Task DeleteDraftAsync(long costId, CancellationToken ct)
    {
        var cost = await LoadForManageAsync(costId, ct);
        cost.EnsureDraft();

        db.CostTransactions.Remove(cost);
        db.AuditEvents.Add(AuditEvent.Create("cost.draft_deleted", "CostTransaction", cost.Id, user.Id, cost.ProjectId, correlation.CorrelationId,
            clock.GetUtcNow(), new { cost.Amount, cost.Description }));
        await db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// F6. Status change, audit row and (when a budget threshold is newly crossed) outbox message are saved together.
    /// If any of them fails the transaction is rolled back and the cost stays a draft.
    /// </summary>
    public async Task<CostDto> PostAsync(long costId, PostCostCommand cmd, CancellationToken ct)
    {
        var cost = await LoadForManageAsync(costId, ct);
        db.Entry(cost).Property(c => c.Version).OriginalValue = cmd.Version;

        var asOf = reporting.Value.DefaultAsOf;
        var kpi = await projects.GetKpisAsync(cost.ProjectId, asOf, ct);   // spend and budget BEFORE this posting
        var now = clock.GetUtcNow();

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        cost.Post(user.Id, now);   // throws already_posted (409) on a second call
        db.AuditEvents.Add(AuditEvent.Create("cost.posted", "CostTransaction", cost.Id, user.Id, cost.ProjectId, correlation.CorrelationId, now,
            new { cost.Amount, cost.CategoryId, cost.TxnDate }));

        if (kpi is { CurrentBudget: > 0 })
        {
            var after = kpi.ActualSpend + (cost.TxnDate <= asOf ? cost.Amount : 0m);
            await QueueThresholdMessagesAsync(cost.ProjectId, kpi.CurrentBudget, kpi.ActualSpend, after, now, ct);
        }

        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return (await mapper.ToDtosAsync([cost], ct)).Single();
    }

    /// <summary>F7. Creates a posted, negative, linked row. One reversal per transaction.</summary>
    public async Task<CostDto> ReverseAsync(long costId, ReverseCostCommand cmd, CancellationToken ct)
    {
        var original = await LoadForManageAsync(costId, ct);
        await reverseValidator.ValidateAndThrowAsync(cmd, ct);
        if (await db.CostTransactions.AnyAsync(c => c.ReversesTransactionId == costId, ct))
        {
            throw new DomainRuleException("already_reversed", "This cost already has a reversal.");
        }

        var now = clock.GetUtcNow();
        var reversal = original.Reverse(cmd.Reason, user.Id, now, Guid.NewGuid());

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        db.CostTransactions.Add(reversal);
        await db.SaveChangesAsync(ct);
        db.AuditEvents.Add(AuditEvent.Create("cost.reversed", "CostTransaction", reversal.Id, user.Id, original.ProjectId, correlation.CorrelationId, now,
            new { reverses = original.Id, amount = reversal.Amount, reason = cmd.Reason.Trim() }));
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return (await mapper.ToDtosAsync([reversal], ct)).Single();
    }

    /// <summary>
    /// H2: utilisation moving from under 90% to 90% or more, or spend moving from within budget to over it,
    /// queues one message per project and threshold, ever (the dedup key is UNIQUE).
    /// </summary>
    private async Task QueueThresholdMessagesAsync(int projectId, decimal budget, decimal before, decimal after, DateTimeOffset now, CancellationToken ct)
    {
        var crossed = new List<int>();
        if (before / budget < 0.90m && after / budget >= 0.90m) crossed.Add(90);
        if (before <= budget && after > budget) crossed.Add(100);

        foreach (var threshold in crossed)
        {
            var key = $"budget:{projectId}:{threshold}";
            if (!await db.OutboxMessages.AnyAsync(o => o.DedupKey == key, ct))
            {
                db.OutboxMessages.Add(OutboxMessage.Create("BudgetThresholdCrossed", key, new { projectId, threshold }, now));
            }
        }
    }

    private async Task<CostTransaction> LoadForManageAsync(long costId, CancellationToken ct)
    {
        var cost = await db.CostTransactions.FirstOrDefaultAsync(c => c.Id == costId, ct) ?? throw new NotFoundException("Cost", costId);
        await scope.EnsureCanManageAsync(cost.ProjectId, ct);
        return cost;
    }

    private async Task EnsureActiveCategoryAsync(int categoryId, CancellationToken ct)
    {
        if (!await db.CostCategories.AnyAsync(c => c.Id == categoryId && c.IsActive, ct))
        {
            throw new DomainRuleException("category_not_found", "Choose an active cost category.", isValidation: true);
        }
    }
}
