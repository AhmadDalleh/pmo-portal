using Microsoft.EntityFrameworkCore;
using Pmo.Application.Common;
using Pmo.Application.Common.Abstractions;
using Pmo.Application.Common.Exceptions;
using Pmo.Domain.Automation;
using Pmo.Domain.Common;

namespace Pmo.Application.Features.Admin;

/// <summary>Stories A5 (audit review) and H3 (automation monitor). Every route that reaches it is Administrator only.</summary>
public sealed class AdminService(IPmoDbContext db, IUserDirectory users, TimeProvider clock)
{
    public async Task<PagedResult<AuditEventDto>> AuditAsync(AuditSearchQuery q, CancellationToken ct)
    {
        var (page, size) = Paging(q.Page, q.PageSize);
        var events = db.AuditEvents.AsNoTracking().AsQueryable();
        if (q.EntityType is not null) events = events.Where(e => e.EntityType == q.EntityType);
        if (q.Action is not null) events = events.Where(e => e.Action == q.Action);
        if (q.ActorId is not null) events = events.Where(e => e.ActorId == q.ActorId);
        if (q.ProjectId is not null) events = events.Where(e => e.ProjectId == q.ProjectId);
        if (q.From is not null)
        {
            var from = new DateTimeOffset(q.From.Value.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
            events = events.Where(e => e.OccurredAt >= from);
        }

        if (q.To is not null)
        {
            var to = new DateTimeOffset(q.To.Value.AddDays(1).ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
            events = events.Where(e => e.OccurredAt < to);
        }

        var total = await events.CountAsync(ct);
        var rows = await events.OrderByDescending(e => e.OccurredAt).ThenByDescending(e => e.Id).Skip((page - 1) * size).Take(size).ToListAsync(ct);
        var names = await users.NamesAsync(rows.Where(e => e.ActorId != null).Select(e => e.ActorId!.Value), ct);
        var items = rows.Select(e => new AuditEventDto(e.Id, e.OccurredAt, e.ActorId, e.ActorId is { } a ? names[a] : null, e.ProjectId,
            e.EntityType, e.EntityId, e.Action, e.CorrelationId, e.Data)).ToList();
        return new PagedResult<AuditEventDto>(items, page, size, total);
    }

    public async Task<PagedResult<AutomationRunDto>> RunsAsync(int pageNumber, int pageSize, CancellationToken ct)
    {
        var (page, size) = Paging(pageNumber, pageSize);
        var total = await db.AutomationRuns.CountAsync(ct);
        var rows = await db.AutomationRuns.AsNoTracking().OrderByDescending(r => r.StartedAt).ThenByDescending(r => r.Id)
            .Skip((page - 1) * size).Take(size)
            .Select(r => new AutomationRunDto(r.Id, r.JobName, r.StartedAt, r.FinishedAt, r.Status, r.MessagesProcessed, r.MessagesFailed, r.Error, r.CorrelationId))
            .ToListAsync(ct);
        return new PagedResult<AutomationRunDto>(rows, page, size, total);
    }

    public async Task<PagedResult<OutboxMessageDto>> OutboxAsync(string? status, int pageNumber, int pageSize, CancellationToken ct)
    {
        if (status is not null && !OutboxStatus.All.Contains(status))
        {
            throw new DomainRuleException("invalid_status", "Status must be pending, processed or failed.", isValidation: true);
        }

        var (page, size) = Paging(pageNumber, pageSize);
        var messages = db.OutboxMessages.AsNoTracking().AsQueryable();
        if (status is not null) messages = messages.Where(m => m.Status == status);

        var total = await messages.CountAsync(ct);
        var rows = await messages.OrderByDescending(m => m.OccurredAt).ThenByDescending(m => m.Id).Skip((page - 1) * size).Take(size)
            .Select(m => new OutboxMessageDto(m.Id, m.Type, m.Payload, m.DedupKey, m.Status, m.OccurredAt, m.AvailableAt, m.Attempts, m.ProcessedAt, m.LastError))
            .ToListAsync(ct);
        return new PagedResult<OutboxMessageDto>(rows, page, size, total);
    }

    /// <summary>H3. Puts a failed message back in the queue; the worker picks it up on its next run.</summary>
    public async Task<OutboxMessageDto> RetryAsync(long id, CancellationToken ct)
    {
        var message = await db.OutboxMessages.FirstOrDefaultAsync(m => m.Id == id, ct) ?? throw new NotFoundException("Outbox message", id);
        if (message.Status != OutboxStatus.Failed)
        {
            throw new DomainRuleException("not_failed", "Only failed messages can be retried.");
        }

        message.Status = OutboxStatus.Pending;
        message.Attempts = 0;
        message.AvailableAt = clock.GetUtcNow();
        message.LastError = null;
        await db.SaveChangesAsync(ct);
        return new OutboxMessageDto(message.Id, message.Type, message.Payload, message.DedupKey, message.Status, message.OccurredAt,
            message.AvailableAt, message.Attempts, message.ProcessedAt, message.LastError);
    }

    private static (int Page, int Size) Paging(int page, int size) => (Math.Max(1, page), Math.Clamp(size, 1, 100));
}
