using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Pmo.Application.Common;
using Pmo.Application.Common.Abstractions;
using Pmo.Application.Common.Exceptions;
using Pmo.Application.Features.Milestones.Commands;
using Pmo.Application.Features.Milestones.Dtos;
using Pmo.Domain.Delivery;
using Pmo.Domain.Governance;

namespace Pmo.Application.Features.Milestones;

/// <summary>Story C5: list, create, edit and complete milestones.</summary>
public sealed class MilestoneService(
    IPmoDbContext db, ICurrentUser user, IProjectScope scope, IOptions<ReportingOptions> reporting, TimeProvider clock,
    ICorrelationIdAccessor correlation, IValidator<CreateMilestoneCommand> createValidator, IValidator<UpdateMilestoneCommand> updateValidator)
{
    public async Task<List<MilestoneDto>> ListAsync(int projectId, DateOnly? asOf, CancellationToken ct)
    {
        await scope.EnsureCanViewAsync(projectId, ct);
        var date = reporting.Value.Resolve(asOf);

        var milestones = await db.Milestones.AsNoTracking().Where(m => m.ProjectId == projectId)
            .OrderBy(m => m.DueDate).ThenBy(m => m.Id).ToListAsync(ct);
        var counts = await db.Tasks.AsNoTracking()
            .Where(t => t.ProjectId == projectId && t.MilestoneId != null && t.Status != ProjectTaskStatus.Cancelled)
            .GroupBy(t => t.MilestoneId!.Value)
            .Select(g => new { MilestoneId = g.Key, Total = g.Count(), Done = g.Count(t => t.Status == ProjectTaskStatus.Done) })
            .ToDictionaryAsync(x => x.MilestoneId, ct);

        return milestones.Select(m => ToDto(m, date, counts.GetValueOrDefault(m.Id)?.Total ?? 0, counts.GetValueOrDefault(m.Id)?.Done ?? 0)).ToList();
    }

    public async Task<MilestoneDto> CreateAsync(int projectId, CreateMilestoneCommand cmd, CancellationToken ct)
    {
        await scope.EnsureCanManageAsync(projectId, ct);
        await createValidator.ValidateAndThrowAsync(cmd, ct);

        var now = clock.GetUtcNow();
        var milestone = new Milestone
        {
            ProjectId = projectId, Name = cmd.Name.Trim(), Description = cmd.Description?.Trim(), DueDate = cmd.DueDate,
            CreatedAt = now, CreatedById = user.Id
        };

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        db.Milestones.Add(milestone);
        await db.SaveChangesAsync(ct);
        db.AuditEvents.Add(AuditEvent.Create("milestone.created", "Milestone", milestone.Id, user.Id, projectId, correlation.CorrelationId, now,
            new { milestone.Name, milestone.DueDate }));
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return ToDto(milestone, reporting.Value.DefaultAsOf, 0, 0);
    }

    public async Task<MilestoneDto> UpdateAsync(int milestoneId, UpdateMilestoneCommand cmd, CancellationToken ct)
    {
        var milestone = await LoadForManageAsync(milestoneId, ct);
        await updateValidator.ValidateAndThrowAsync(cmd, ct);
        db.Entry(milestone).Property(m => m.Version).OriginalValue = cmd.Version;

        var now = clock.GetUtcNow();
        var before = new { milestone.Name, milestone.DueDate };
        milestone.Name = cmd.Name.Trim();
        milestone.Description = cmd.Description?.Trim();
        milestone.DueDate = cmd.DueDate;
        milestone.UpdatedAt = now;
        milestone.UpdatedById = user.Id;

        db.AuditEvents.Add(AuditEvent.Create("milestone.updated", "Milestone", milestone.Id, user.Id, milestone.ProjectId, correlation.CorrelationId, now,
            new { before, after = new { milestone.Name, milestone.DueDate } }));
        await db.SaveChangesAsync(ct);
        return await ToDtoWithCountsAsync(milestone, ct);
    }

    public async Task<MilestoneDto> CompleteAsync(int milestoneId, CompleteMilestoneCommand cmd, CancellationToken ct)
    {
        var milestone = await LoadForManageAsync(milestoneId, ct);
        db.Entry(milestone).Property(m => m.Version).OriginalValue = cmd.Version;

        var now = clock.GetUtcNow();
        milestone.Complete(DateOnly.FromDateTime(now.UtcDateTime));   // throws already_completed (409) on the second call
        milestone.UpdatedAt = now;
        milestone.UpdatedById = user.Id;

        db.AuditEvents.Add(AuditEvent.Create("milestone.completed", "Milestone", milestone.Id, user.Id, milestone.ProjectId,
            correlation.CorrelationId, now, new { milestone.Name }));
        await db.SaveChangesAsync(ct);
        return await ToDtoWithCountsAsync(milestone, ct);
    }

    private async Task<Milestone> LoadForManageAsync(int milestoneId, CancellationToken ct)
    {
        var milestone = await db.Milestones.FirstOrDefaultAsync(m => m.Id == milestoneId, ct) ?? throw new NotFoundException("Milestone", milestoneId);
        await scope.EnsureCanManageAsync(milestone.ProjectId, ct);
        return milestone;
    }

    private async Task<MilestoneDto> ToDtoWithCountsAsync(Milestone m, CancellationToken ct)
    {
        var tasks = await db.Tasks.AsNoTracking().Where(t => t.MilestoneId == m.Id && t.Status != ProjectTaskStatus.Cancelled)
            .Select(t => t.Status).ToListAsync(ct);
        return ToDto(m, reporting.Value.DefaultAsOf, tasks.Count, tasks.Count(s => s == ProjectTaskStatus.Done));
    }

    private static MilestoneDto ToDto(Milestone m, DateOnly asOf, int total, int done) => new(
        m.Id, m.ProjectId, m.Name, m.Description, m.DueDate, m.Status, m.CompletedAt,
        m.Status == MilestoneStatus.Pending && m.DueDate < asOf, total, done, m.Version);
}
