using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Pmo.Application.Common;
using Pmo.Application.Common.Abstractions;
using Pmo.Application.Common.Exceptions;
using Pmo.Application.Features.ChangeRequests.Commands;
using Pmo.Application.Features.ChangeRequests.Dtos;
using Pmo.Application.Features.ChangeRequests.Queries;
using Pmo.Domain.Automation;
using Pmo.Domain.Governance;

namespace Pmo.Application.Features.ChangeRequests;

/// <summary>
/// Epic E: raise, edit, submit, withdraw and decide change requests. Every status change goes through the entity
/// (state machine) and is saved together with exactly one history row and one audit event.
/// </summary>
public sealed class ChangeRequestService(
    IPmoDbContext db, ICurrentUser user, IProjectScope scope, IUserDirectory users, TimeProvider clock, ICorrelationIdAccessor correlation,
    IValidator<CreateChangeRequestCommand> createValidator, IValidator<UpdateChangeRequestCommand> updateValidator,
    IValidator<DecideChangeRequestCommand> decideValidator, IValidator<ChangeRequestSearchQuery> searchValidator)
{
    public async Task<PagedResult<ChangeRequestDto>> ListForProjectAsync(int projectId, ChangeRequestSearchQuery query, CancellationToken ct)
    {
        await scope.EnsureCanViewAsync(projectId, ct);
        await searchValidator.ValidateAndThrowAsync(query, ct);

        var requests = db.ChangeRequests.AsNoTracking().Where(c => c.ProjectId == projectId);
        if (query.Status is not null) requests = requests.Where(c => c.Status == query.Status);
        return await PageAsync(requests.OrderByDescending(c => c.CreatedAt).ThenByDescending(c => c.Id), query, ct);
    }

    /// <summary>
    /// E2. Submitted requests of projects the caller manages, never their own. Oldest first, because the oldest waits longest.
    /// </summary>
    public async Task<PagedResult<ChangeRequestDto>> ReviewQueueAsync(ChangeRequestSearchQuery query, CancellationToken ct)
    {
        await searchValidator.ValidateAndThrowAsync(query, ct);
        var managed = await scope.ManagedProjectIdsAsync(ct);
        var status = query.Status ?? ChangeRequestStatus.Submitted;

        var requests = db.ChangeRequests.AsNoTracking()
            .Where(c => managed.Contains(c.ProjectId) && c.RequestedById != user.Id && c.Status == status);
        if (query.ProjectId is not null) requests = requests.Where(c => c.ProjectId == query.ProjectId);
        return await PageAsync(requests.OrderBy(c => c.SubmittedAt).ThenBy(c => c.Id), query, ct);
    }

    public async Task<ChangeRequestDto> GetAsync(int id, CancellationToken ct)
    {
        var cr = await db.ChangeRequests.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id, ct) ?? throw new NotFoundException("Change request", id);
        await scope.EnsureCanViewAsync(cr.ProjectId, ct);
        return (await ToDtosAsync([cr], ct)).Single();
    }

    /// <summary>E4. The full timeline, oldest first.</summary>
    public async Task<List<ChangeRequestHistoryDto>> HistoryAsync(int id, CancellationToken ct)
    {
        var cr = await db.ChangeRequests.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id, ct) ?? throw new NotFoundException("Change request", id);
        await scope.EnsureCanViewAsync(cr.ProjectId, ct);

        var rows = await db.ChangeRequestHistory.AsNoTracking().Where(h => h.ChangeRequestId == id).OrderBy(h => h.OccurredAt).ThenBy(h => h.Id).ToListAsync(ct);
        var names = await users.NamesAsync(rows.Select(h => h.ActorId), ct);
        return rows.Select(h => new ChangeRequestHistoryDto(h.Id, h.FromStatus, h.ToStatus, h.ActorId, names[h.ActorId], h.Reason, h.OccurredAt)).ToList();
    }

    /// <summary>E1. Any member of the project (or an administrator) may raise one. It starts as a draft.</summary>
    public async Task<ChangeRequestDto> CreateAsync(int projectId, CreateChangeRequestCommand cmd, CancellationToken ct)
    {
        await scope.EnsureCanViewAsync(projectId, ct);
        await createValidator.ValidateAndThrowAsync(cmd, ct);

        var now = clock.GetUtcNow();
        var cr = new ChangeRequest
        {
            Code = "TMP-" + Guid.NewGuid().ToString("N")[..12],   // replaced below, once the database has assigned the id
            ProjectId = projectId, Title = cmd.Title.Trim(), Description = cmd.Description.Trim(), Type = cmd.Type,
            CostImpact = cmd.CostImpact, ScheduleImpactDays = cmd.ScheduleImpactDays, Status = ChangeRequestStatus.Draft,
            RequestedById = user.Id, CreatedAt = now, CreatedById = user.Id
        };

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        db.ChangeRequests.Add(cr);
        await db.SaveChangesAsync(ct);
        cr.Code = $"CR-{cr.Id:D4}";
        db.ChangeRequestHistory.Add(cr.CreationHistory(now, correlation.CorrelationId));
        db.AuditEvents.Add(AuditEvent.Create("change_request.created", "ChangeRequest", cr.Id, user.Id, projectId, correlation.CorrelationId, now,
            new { cr.Code, cr.Type }));
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return (await ToDtosAsync([cr], ct)).Single();
    }

    public async Task<ChangeRequestDto> UpdateAsync(int id, UpdateChangeRequestCommand cmd, CancellationToken ct)
    {
        var cr = await LoadVisibleAsync(id, ct);
        cr.EnsureEditableBy(user.Id);   // requester only, draft or returned only
        await updateValidator.ValidateAndThrowAsync(cmd, ct);
        db.Entry(cr).Property(c => c.Version).OriginalValue = cmd.Version;

        var now = clock.GetUtcNow();
        cr.Title = cmd.Title.Trim();
        cr.Description = cmd.Description.Trim();
        cr.Type = cmd.Type;
        cr.CostImpact = cmd.CostImpact;
        cr.ScheduleImpactDays = cmd.ScheduleImpactDays;
        cr.UpdatedAt = now;
        cr.UpdatedById = user.Id;

        db.AuditEvents.Add(AuditEvent.Create("change_request.updated", "ChangeRequest", cr.Id, user.Id, cr.ProjectId, correlation.CorrelationId, now));
        await db.SaveChangesAsync(ct);
        return (await ToDtosAsync([cr], ct)).Single();
    }

    public async Task<ChangeRequestDto> SubmitAsync(int id, SubmitChangeRequestCommand cmd, CancellationToken ct)
    {
        var cr = await LoadVisibleAsync(id, ct);
        db.Entry(cr).Property(c => c.Version).OriginalValue = cmd.Version;
        var now = clock.GetUtcNow();

        db.ChangeRequestHistory.Add(cr.Submit(user.Id, now, correlation.CorrelationId));
        cr.UpdatedAt = now;
        cr.UpdatedById = user.Id;
        db.AuditEvents.Add(AuditEvent.Create("change_request.submitted", "ChangeRequest", cr.Id, user.Id, cr.ProjectId, correlation.CorrelationId, now));
        await db.SaveChangesAsync(ct);   // status + history + audit: one transaction
        return (await ToDtosAsync([cr], ct)).Single();
    }

    public async Task<ChangeRequestDto> WithdrawAsync(int id, WithdrawChangeRequestCommand cmd, CancellationToken ct)
    {
        var cr = await LoadVisibleAsync(id, ct);
        db.Entry(cr).Property(c => c.Version).OriginalValue = cmd.Version;
        var now = clock.GetUtcNow();

        db.ChangeRequestHistory.Add(cr.Withdraw(user.Id, cmd.Reason, now, correlation.CorrelationId));
        cr.UpdatedAt = now;
        cr.UpdatedById = user.Id;
        db.AuditEvents.Add(AuditEvent.Create("change_request.withdrawn", "ChangeRequest", cr.Id, user.Id, cr.ProjectId, correlation.CorrelationId, now));
        await db.SaveChangesAsync(ct);
        return (await ToDtosAsync([cr], ct)).Single();
    }

    /// <summary>
    /// E3. The route needs the CanDecide role; here the caller must also manage the request's project, and the entity refuses
    /// the requester. Change request, ONE history row, audit event and (on approval) the outbox message are saved in one
    /// transaction: if any insert fails nothing is saved and the status is unchanged.
    /// </summary>
    public async Task<ChangeRequestDto> DecideAsync(int id, DecideChangeRequestCommand cmd, CancellationToken ct)
    {
        var cr = await db.ChangeRequests.FirstOrDefaultAsync(c => c.Id == id, ct) ?? throw new NotFoundException("Change request", id);
        await scope.EnsureCanManageAsync(cr.ProjectId, ct);
        await decideValidator.ValidateAndThrowAsync(cmd, ct);
        db.Entry(cr).Property(c => c.Version).OriginalValue = cmd.Version;

        var decision = cmd.Decision switch
        {
            "approve" => ChangeRequestStatus.Approved,
            "reject" => ChangeRequestStatus.Rejected,
            _ => ChangeRequestStatus.Returned
        };
        var now = clock.GetUtcNow();

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        db.ChangeRequestHistory.Add(cr.Decide(decision, user.Id, cmd.Reason, now, correlation.CorrelationId));
        cr.UpdatedAt = now;
        cr.UpdatedById = user.Id;
        db.AuditEvents.Add(AuditEvent.Create("change_request.decided", "ChangeRequest", cr.Id, user.Id, cr.ProjectId, correlation.CorrelationId, now,
            new { decision, reason = cmd.Reason.Trim() }));
        if (decision == ChangeRequestStatus.Approved)
        {
            db.OutboxMessages.Add(OutboxMessage.Create("ChangeRequestApproved", $"cr-approved:{cr.Id}", new { changeRequestId = cr.Id }, now));
        }

        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return (await ToDtosAsync([cr], ct)).Single();
    }

    private async Task<ChangeRequest> LoadVisibleAsync(int id, CancellationToken ct)
    {
        var cr = await db.ChangeRequests.FirstOrDefaultAsync(c => c.Id == id, ct) ?? throw new NotFoundException("Change request", id);
        await scope.EnsureCanViewAsync(cr.ProjectId, ct);
        return cr;
    }

    private async Task<PagedResult<ChangeRequestDto>> PageAsync(IQueryable<ChangeRequest> ordered, ChangeRequestSearchQuery query, CancellationToken ct)
    {
        var total = await ordered.CountAsync(ct);
        var page = await ordered.Skip((query.Page - 1) * query.PageSize).Take(query.PageSize).ToListAsync(ct);
        return new PagedResult<ChangeRequestDto>(await ToDtosAsync(page, ct), query.Page, query.PageSize, total);
    }

    private async Task<List<ChangeRequestDto>> ToDtosAsync(IReadOnlyCollection<ChangeRequest> requests, CancellationToken ct)
    {
        var projectIds = requests.Select(c => c.ProjectId).Distinct().ToArray();
        var codes = await db.Projects.AsNoTracking().Where(p => projectIds.Contains(p.Id)).ToDictionaryAsync(p => p.Id, p => p.Code, ct);
        var names = await users.NamesAsync(requests.Select(c => c.RequestedById).Concat(requests.Where(c => c.DecidedById != null).Select(c => c.DecidedById!.Value)), ct);

        return requests.Select(c => new ChangeRequestDto(
            c.Id, c.Code, c.ProjectId, codes[c.ProjectId], c.Title, c.Description, c.Type, c.CostImpact, c.ScheduleImpactDays, c.Status,
            c.RequestedById, names[c.RequestedById], c.CreatedAt, c.SubmittedAt, c.DecidedById is { } d ? names[d] : null,
            c.DecidedAt, c.DecisionReason, c.Version)).ToList();
    }
}
