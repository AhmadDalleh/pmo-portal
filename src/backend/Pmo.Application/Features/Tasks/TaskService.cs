using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Pmo.Application.Common;
using Pmo.Application.Common.Abstractions;
using Pmo.Application.Common.Exceptions;
using Pmo.Application.Features.Tasks.Commands;
using Pmo.Application.Features.Tasks.Dtos;
using Pmo.Application.Features.Tasks.Queries;
using Pmo.Domain.Common;
using Pmo.Domain.Delivery;
using Pmo.Domain.Governance;

namespace Pmo.Application.Features.Tasks;

/// <summary>Stories C1-C4: list, create, edit, update progress, "my work".</summary>
public sealed class TaskService(
    IPmoDbContext db, ICurrentUser user, IProjectScope scope, TaskMapper mapper, IOptions<ReportingOptions> reporting,
    TimeProvider clock, ICorrelationIdAccessor correlation, IValidator<CreateTaskCommand> createValidator,
    IValidator<UpdateTaskCommand> updateValidator, IValidator<UpdateTaskProgressCommand> progressValidator,
    IValidator<TaskSearchQuery> searchValidator, IValidator<MyTasksQuery> myTasksValidator)
{
    public async Task<PagedResult<TaskDto>> ListAsync(int projectId, TaskSearchQuery query, DateOnly? asOf, CancellationToken ct)
    {
        await scope.EnsureCanViewAsync(projectId, ct);
        await searchValidator.ValidateAndThrowAsync(query, ct);
        var date = reporting.Value.Resolve(asOf);

        var tasks = db.Tasks.AsNoTracking().Where(t => t.ProjectId == projectId);
        if (query.Status is not null) tasks = tasks.Where(t => t.Status == query.Status);
        if (query.AssigneeId is not null) tasks = tasks.Where(t => t.AssigneeId == query.AssigneeId);
        if (query.Priority is not null) tasks = tasks.Where(t => t.Priority == query.Priority);
        if (query.Overdue == true) tasks = tasks.Where(TaskRules.Overdue(date));

        var total = await tasks.CountAsync(ct);
        var page = await Sort(tasks, query).Skip((query.Page - 1) * query.PageSize).Take(query.PageSize).ToListAsync(ct);
        return new PagedResult<TaskDto>(await mapper.ToDtosAsync(page, date, ct), query.Page, query.PageSize, total);
    }

    /// <summary>C3. Tasks assigned to the caller across the projects they can still see.</summary>
    public async Task<PagedResult<TaskDto>> MyTasksAsync(MyTasksQuery query, DateOnly? asOf, CancellationToken ct)
    {
        await myTasksValidator.ValidateAndThrowAsync(query, ct);
        var date = reporting.Value.Resolve(asOf);
        var projectIds = await scope.AccessibleProjectIdsAsync(ct);

        var tasks = db.Tasks.AsNoTracking().Where(t => t.AssigneeId == user.Id && projectIds.Contains(t.ProjectId));
        tasks = query.Status switch
        {
            null => tasks.Where(t => t.Status == ProjectTaskStatus.Todo || t.Status == ProjectTaskStatus.InProgress || t.Status == ProjectTaskStatus.Blocked),
            "all" => tasks,
            _ => tasks.Where(t => t.Status == query.Status)
        };

        var total = await tasks.CountAsync(ct);
        var page = await tasks.OrderBy(t => t.DueDate).ThenBy(t => t.Id)
            .Skip((query.Page - 1) * query.PageSize).Take(query.PageSize).ToListAsync(ct);
        return new PagedResult<TaskDto>(await mapper.ToDtosAsync(page, date, ct), query.Page, query.PageSize, total);
    }

    public async Task<TaskDto> GetAsync(int taskId, DateOnly? asOf, CancellationToken ct)
    {
        var task = await db.Tasks.AsNoTracking().FirstOrDefaultAsync(t => t.Id == taskId, ct) ?? throw new NotFoundException("Task", taskId);
        await scope.EnsureCanViewAsync(task.ProjectId, ct);
        return (await mapper.ToDtosAsync([task], reporting.Value.Resolve(asOf), ct)).Single();
    }

    public async Task<TaskDto> CreateAsync(int projectId, CreateTaskCommand cmd, CancellationToken ct)
    {
        await scope.EnsureCanManageAsync(projectId, ct);
        await createValidator.ValidateAndThrowAsync(cmd, ct);
        await EnsureAssigneeAndMilestoneAsync(projectId, cmd.AssigneeId, cmd.MilestoneId, ct);

        var now = clock.GetUtcNow();
        var task = new ProjectTask
        {
            ProjectId = projectId, Title = cmd.Title.Trim(), Description = cmd.Description?.Trim(), AssigneeId = cmd.AssigneeId,
            MilestoneId = cmd.MilestoneId, Priority = cmd.Priority, StartDate = cmd.StartDate, DueDate = cmd.DueDate,
            Weight = (short)cmd.Weight, CreatedAt = now, CreatedById = user.Id
        };
        task.ApplyProgress(cmd.Status, (short)cmd.ProgressPct, now);   // done => progress 100 + completed_at

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        db.Tasks.Add(task);
        await db.SaveChangesAsync(ct);   // first save: the database assigns task.Id, which the audit row needs
        db.AuditEvents.Add(AuditEvent.Create("task.created", "Task", task.Id, user.Id, projectId, correlation.CorrelationId, now,
            new { task.Title, task.Status, task.AssigneeId }));
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return await ToDtoAsync(task, ct);
    }

    public async Task<TaskDto> UpdateAsync(int taskId, UpdateTaskCommand cmd, CancellationToken ct)
    {
        var task = await db.Tasks.FirstOrDefaultAsync(t => t.Id == taskId, ct) ?? throw new NotFoundException("Task", taskId);
        await scope.EnsureCanManageAsync(task.ProjectId, ct);
        await updateValidator.ValidateAndThrowAsync(cmd, ct);
        await EnsureAssigneeAndMilestoneAsync(task.ProjectId, cmd.AssigneeId, cmd.MilestoneId, ct);
        db.Entry(task).Property(t => t.Version).OriginalValue = cmd.Version;

        var now = clock.GetUtcNow();
        var before = new { task.Status, task.ProgressPct, task.AssigneeId, task.DueDate };
        task.Title = cmd.Title.Trim();
        task.Description = cmd.Description?.Trim();
        task.AssigneeId = cmd.AssigneeId;
        task.MilestoneId = cmd.MilestoneId;
        task.Priority = cmd.Priority;
        task.StartDate = cmd.StartDate;
        task.DueDate = cmd.DueDate;
        task.Weight = (short)cmd.Weight;
        task.ApplyProgress(cmd.Status, (short)cmd.ProgressPct, now);
        task.UpdatedAt = now;
        task.UpdatedById = user.Id;

        db.AuditEvents.Add(AuditEvent.Create("task.updated", "Task", task.Id, user.Id, task.ProjectId, correlation.CorrelationId, now,
            new { before, after = new { task.Status, task.ProgressPct, task.AssigneeId, task.DueDate } }));
        await db.SaveChangesAsync(ct);
        return await ToDtoAsync(task, ct);
    }

    /// <summary>
    /// C2. Managers may update any task of their project; anyone else only their own assigned task, and never to "cancelled".
    /// Project progress and the dashboard change on the next read because they are calculated in SQL.
    /// </summary>
    public async Task<TaskDto> UpdateProgressAsync(int taskId, UpdateTaskProgressCommand cmd, CancellationToken ct)
    {
        var task = await db.Tasks.FirstOrDefaultAsync(t => t.Id == taskId, ct) ?? throw new NotFoundException("Task", taskId);
        await scope.EnsureCanViewAsync(task.ProjectId, ct);   // 404 for tasks of projects outside the caller's scope
        await progressValidator.ValidateAndThrowAsync(cmd, ct);

        if (!await scope.CanManageAsync(task.ProjectId, ct))
        {
            if (task.AssigneeId != user.Id)
            {
                throw new ForbiddenException("You can only update tasks assigned to you.");
            }

            if (cmd.Status == ProjectTaskStatus.Cancelled || task.Status == ProjectTaskStatus.Cancelled)
            {
                throw new ForbiddenException("Only a project manager can cancel or reopen a task.");
            }
        }

        db.Entry(task).Property(t => t.Version).OriginalValue = cmd.Version;
        var now = clock.GetUtcNow();
        var before = new { task.Status, task.ProgressPct };
        task.ApplyProgress(cmd.Status, (short)cmd.ProgressPct, now);
        task.UpdatedAt = now;
        task.UpdatedById = user.Id;

        db.AuditEvents.Add(AuditEvent.Create("task.progress_updated", "Task", task.Id, user.Id, task.ProjectId, correlation.CorrelationId, now,
            new { before, after = new { task.Status, task.ProgressPct } }));
        await db.SaveChangesAsync(ct);
        return await ToDtoAsync(task, ct);
    }

    private async Task EnsureAssigneeAndMilestoneAsync(int projectId, int? assigneeId, int? milestoneId, CancellationToken ct)
    {
        if (assigneeId is not null && !await db.ProjectMembers.AnyAsync(m => m.ProjectId == projectId && m.UserId == assigneeId, ct))
        {
            throw new DomainRuleException("assignee_not_member", "The assignee must be a member of the project.", isValidation: true);
        }

        if (milestoneId is not null && !await db.Milestones.AnyAsync(m => m.Id == milestoneId && m.ProjectId == projectId, ct))
        {
            throw new DomainRuleException("milestone_not_in_project", "The milestone must belong to the same project.", isValidation: true);
        }
    }

    private async Task<TaskDto> ToDtoAsync(ProjectTask task, CancellationToken ct) =>
        (await mapper.ToDtosAsync([task], reporting.Value.DefaultAsOf, ct)).Single();

    /// <summary>Sort key (validated against an allow-list) to ORDER BY. Id breaks ties so paging is stable.</summary>
    private static IQueryable<ProjectTask> Sort(IQueryable<ProjectTask> tasks, TaskSearchQuery q)
    {
        var desc = q.Dir == "desc";
        return q.Sort switch
        {
            "title" => Order(tasks, t => t.Title, desc),
            "status" => Order(tasks, t => t.Status, desc),
            "progress" => Order(tasks, t => t.ProgressPct, desc),
            "weight" => Order(tasks, t => t.Weight, desc),
            "priority" => Order(tasks, t => t.Priority == ProjectTaskPriority.Critical ? 4 : t.Priority == ProjectTaskPriority.High ? 3
                : t.Priority == ProjectTaskPriority.Medium ? 2 : 1, desc),
            _ => Order(tasks, t => t.DueDate, desc)
        };
    }

    private static IQueryable<ProjectTask> Order<TKey>(IQueryable<ProjectTask> tasks, System.Linq.Expressions.Expression<Func<ProjectTask, TKey>> key, bool desc) =>
        (desc ? tasks.OrderByDescending(key) : tasks.OrderBy(key)).ThenBy(t => t.Id);
}
