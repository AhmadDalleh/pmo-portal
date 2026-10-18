using Microsoft.EntityFrameworkCore;
using Pmo.Application.Common.Abstractions;
using Pmo.Application.Features.Tasks.Dtos;
using Pmo.Application.Features.Tasks.Queries;
using Pmo.Domain.Delivery;

namespace Pmo.Application.Features.Tasks;

/// <summary>Turns tasks into DTOs, loading the project codes, milestone names and assignee names in three small queries (no N+1).</summary>
public sealed class TaskMapper(IPmoDbContext db, IUserDirectory users)
{
    public async Task<List<TaskDto>> ToDtosAsync(IReadOnlyCollection<ProjectTask> tasks, DateOnly asOf, CancellationToken ct)
    {
        var projectIds = tasks.Select(t => t.ProjectId).Distinct().ToArray();
        var milestoneIds = tasks.Where(t => t.MilestoneId != null).Select(t => t.MilestoneId!.Value).Distinct().ToArray();

        var codes = await db.Projects.AsNoTracking().Where(p => projectIds.Contains(p.Id)).ToDictionaryAsync(p => p.Id, p => p.Code, ct);
        var milestones = await db.Milestones.AsNoTracking().Where(m => milestoneIds.Contains(m.Id)).ToDictionaryAsync(m => m.Id, m => m.Name, ct);
        var people = await users.NamesAsync(tasks.Where(t => t.AssigneeId != null).Select(t => t.AssigneeId!.Value), ct);

        return tasks.Select(t => new TaskDto(
            t.Id, t.ProjectId, codes[t.ProjectId], t.MilestoneId, t.MilestoneId is { } m ? milestones[m] : null, t.Title, t.Description,
            t.AssigneeId, t.AssigneeId is { } a ? people[a] : null, t.Status, t.Priority, t.StartDate, t.DueDate,
            t.Weight, t.ProgressPct, t.CompletedAt, TaskRules.Indicator(t, asOf), t.Version)).ToList();
    }
}
