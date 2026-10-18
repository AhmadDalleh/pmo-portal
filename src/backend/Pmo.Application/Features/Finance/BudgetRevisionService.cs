using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Pmo.Application.Common.Abstractions;
using Pmo.Application.Common.Exceptions;
using Pmo.Application.Features.Finance.Commands;
using Pmo.Application.Features.Finance.Dtos;
using Pmo.Domain.Common;
using Pmo.Domain.Finance;
using Pmo.Domain.Governance;

namespace Pmo.Application.Features.Finance;

/// <summary>Stories F3 and F4: request a dated budget revision (project manager) and decide it (administrator).</summary>
public sealed class BudgetRevisionService(
    IPmoDbContext db, ICurrentUser user, IProjectScope scope, IUserDirectory users, TimeProvider clock,
    ICorrelationIdAccessor correlation, IValidator<RequestRevisionCommand> requestValidator, IValidator<DecideRevisionCommand> decideValidator)
{
    public async Task<List<BudgetRevisionDto>> ListAsync(int projectId, CancellationToken ct)
    {
        await scope.EnsureCanViewAsync(projectId, ct);
        var revisions = await db.BudgetRevisions.AsNoTracking().Where(r => r.ProjectId == projectId)
            .OrderByDescending(r => r.RequestedAt).ThenByDescending(r => r.Id).ToListAsync(ct);
        return await ToDtosAsync(revisions, ct);
    }

    public async Task<BudgetRevisionDto> RequestAsync(int projectId, RequestRevisionCommand cmd, CancellationToken ct)
    {
        await scope.EnsureCanManageAsync(projectId, ct);
        await requestValidator.ValidateAndThrowAsync(cmd, ct);

        if (cmd.ChangeRequestId is { } crId)
        {
            var cr = await db.ChangeRequests.AsNoTracking().FirstOrDefaultAsync(c => c.Id == crId, ct)
                     ?? throw new DomainRuleException("change_request_not_found", "The linked change request does not exist.", isValidation: true);
            if (cr.ProjectId != projectId)
            {
                throw new DomainRuleException("change_request_other_project", "The change request belongs to another project.", isValidation: true);
            }

            if (cr.Status != ChangeRequestStatus.Approved)
            {
                throw new DomainRuleException("change_request_not_approved", "Only an approved change request can be linked.", isValidation: true);
            }
        }

        // A reduction may not push the budget below zero (initial amount + every approved revision + this one).
        var approvedTotal = await db.BudgetRevisions.Where(r => r.ProjectId == projectId && r.Status == RevisionStatus.Approved)
            .SumAsync(r => (decimal?)r.Amount, ct) ?? 0m;
        var initial = await db.ProjectBudgets.Where(b => b.ProjectId == projectId).Select(b => b.InitialAmount).FirstAsync(ct);
        if (initial + approvedTotal + cmd.Amount < 0)
        {
            throw new DomainRuleException("budget_would_be_negative", "This reduction would make the budget negative.", isValidation: true);
        }

        var now = clock.GetUtcNow();
        var revision = new BudgetRevision
        {
            ProjectId = projectId, Amount = cmd.Amount, Reason = cmd.Reason.Trim(), EffectiveDate = cmd.EffectiveDate,
            Status = RevisionStatus.Pending, ChangeRequestId = cmd.ChangeRequestId, RequestedById = user.Id, RequestedAt = now
        };

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        db.BudgetRevisions.Add(revision);
        await db.SaveChangesAsync(ct);
        db.AuditEvents.Add(AuditEvent.Create("budget_revision.requested", "BudgetRevision", revision.Id, user.Id, projectId,
            correlation.CorrelationId, now, new { cmd.Amount, cmd.EffectiveDate, cmd.ChangeRequestId }));
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return (await ToDtosAsync([revision], ct)).Single();
    }

    /// <summary>The route is Administrator only; the entity then refuses self-approval, repeated decisions and a missing reason.</summary>
    public async Task<BudgetRevisionDto> DecideAsync(int revisionId, DecideRevisionCommand cmd, CancellationToken ct)
    {
        var revision = await db.BudgetRevisions.FirstOrDefaultAsync(r => r.Id == revisionId, ct) ?? throw new NotFoundException("Budget revision", revisionId);
        await decideValidator.ValidateAndThrowAsync(cmd, ct);
        db.Entry(revision).Property(r => r.Version).OriginalValue = cmd.Version;

        var now = clock.GetUtcNow();
        revision.Decide(cmd.Decision, user.Id, cmd.Reason, now);
        db.AuditEvents.Add(AuditEvent.Create("budget_revision.decided", "BudgetRevision", revision.Id, user.Id, revision.ProjectId,
            correlation.CorrelationId, now, new { cmd.Decision, cmd.Reason, revision.Amount }));
        await db.SaveChangesAsync(ct);   // revision + audit row, one transaction
        return (await ToDtosAsync([revision], ct)).Single();
    }

    private async Task<List<BudgetRevisionDto>> ToDtosAsync(IReadOnlyCollection<BudgetRevision> revisions, CancellationToken ct)
    {
        var names = await users.NamesAsync(revisions.Select(r => r.RequestedById).Concat(revisions.Where(r => r.DecidedById != null).Select(r => r.DecidedById!.Value)), ct);
        return revisions.Select(r => new BudgetRevisionDto(
            r.Id, r.ProjectId, r.Amount, r.Reason, r.EffectiveDate, r.Status, r.ChangeRequestId, names[r.RequestedById], r.RequestedAt,
            r.DecidedById is { } d ? names[d] : null, r.DecidedAt, r.DecisionReason, r.Version)).ToList();
    }
}
