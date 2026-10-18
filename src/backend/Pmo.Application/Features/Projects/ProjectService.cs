using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Pmo.Application.Common;
using Pmo.Application.Common.Abstractions;
using Pmo.Application.Common.Exceptions;
using Pmo.Application.Features.Projects.Commands;
using Pmo.Application.Features.Projects.Dtos;
using Pmo.Application.Features.Projects.Queries;
using Pmo.Domain.Common;
using Pmo.Domain.Finance;
using Pmo.Domain.Governance;
using Pmo.Domain.Portfolio;

namespace Pmo.Application.Features.Projects;

/// <summary>Stories B1-B4: list, details, summary, create and edit projects.</summary>
public sealed class ProjectService(
    IPmoDbContext db, ICurrentUser user, IProjectScope scope, IUserDirectory users, IProjectReadModel readModel,
    IOptions<ReportingOptions> reporting, TimeProvider clock, ICorrelationIdAccessor correlation,
    IValidator<CreateProjectCommand> createValidator, IValidator<UpdateProjectCommand> updateValidator,
    IValidator<ProjectSearchQuery> searchValidator)
{
    public async Task<PagedResult<ProjectListItemDto>> ListAsync(ProjectSearchQuery query, DateOnly? asOf, CancellationToken ct)
    {
        await searchValidator.ValidateAndThrowAsync(query, ct);
        var scopeIds = await scope.AccessibleProjectIdsAsync(ct);   // scope first: the SQL can only ever see these ids
        var rows = await readModel.SearchAsync(query, scopeIds, reporting.Value.Resolve(asOf), ct);
        var items = rows.Select(r => new ProjectListItemDto(
            r.Id, r.Code, r.Name, r.DepartmentName, r.OwnerName, r.Status, r.StartDate, r.PlannedEndDate,
            r.ProgressPct, r.Utilization, ProjectHealth.Evaluate(r), r.NearBudget, r.Overspent, r.ProjectedOverrun, r.NoForecast)).ToList();
        return new PagedResult<ProjectListItemDto>(items, query.Page, query.PageSize, rows.FirstOrDefault()?.TotalCount ?? 0);
    }

    public async Task<ProjectDto> GetAsync(int id, CancellationToken ct)
    {
        await scope.EnsureCanViewAsync(id, ct);
        var project = await db.Projects.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id, ct)
                      ?? throw new NotFoundException("Project", id);
        return await ToDtoAsync(project, ct);
    }

    public async Task<ProjectSummaryDto> GetSummaryAsync(int id, DateOnly? asOf, CancellationToken ct)
    {
        await scope.EnsureCanViewAsync(id, ct);
        var date = reporting.Value.Resolve(asOf);
        var kpi = await readModel.GetKpisAsync(id, date, ct) ?? throw new NotFoundException("Project", id);
        var next = await readModel.NextMilestoneAsync(id, date, ct);

        return new ProjectSummaryDto(
            id, date, ProjectHealth.Evaluate(kpi),
            new DeliverySummary(kpi.ProgressPct, kpi.OverdueTasks, next),
            new GovernanceSummary(kpi.OpenHighRisks, kpi.PendingChangeRequests),
            new FinanceSummary(kpi.CurrentBudget, kpi.ActualSpend, kpi.RemainingBudget, kpi.Utilization, kpi.ForecastToComplete,
                kpi.Eac, kpi.ProjectedVariance, kpi.NearBudget, kpi.Overspent, kpi.ProjectedOverrun, kpi.NoForecast, kpi.NoBudget));
    }

    /// <summary>B1. Project + budget + owner membership + audit event are saved in one transaction.</summary>
    public async Task<ProjectDto> CreateAsync(CreateProjectCommand cmd, CancellationToken ct)
    {
        await createValidator.ValidateAndThrowAsync(cmd, ct);
        var ownerId = ResolveOwnerId(cmd.OwnerId);
        await EnsureProjectManagerAsync(ownerId, ct);

        if (!await db.Departments.AnyAsync(d => d.Id == cmd.DepartmentId && d.IsActive, ct))
        {
            throw new DomainRuleException("department_not_found", "The department does not exist.", isValidation: true);
        }

        var code = cmd.Code.Trim().ToUpperInvariant();
        if (await db.Projects.AnyAsync(p => p.Code == code, ct))
        {
            throw new DomainRuleException("duplicate_project_code", $"Project code {code} is already used.");
        }

        var now = clock.GetUtcNow();
        var project = new Project
        {
            Code = code, Name = cmd.Name.Trim(), Description = cmd.Description?.Trim(), DepartmentId = cmd.DepartmentId,
            OwnerId = ownerId, Status = ProjectStatus.Proposed, StartDate = cmd.StartDate, PlannedEndDate = cmd.PlannedEndDate,
            CreatedAt = now, CreatedById = user.Id
        };

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        db.Projects.Add(project);
        await db.SaveChangesAsync(ct);   // first save: the database assigns project.Id

        db.ProjectBudgets.Add(new ProjectBudget
        {
            ProjectId = project.Id, InitialAmount = cmd.InitialBudget, Currency = "AED", ApprovedById = user.Id, ApprovedAt = now
        });
        db.ProjectMembers.Add(new ProjectMember { ProjectId = project.Id, UserId = ownerId, MemberRole = MemberRole.Manager, AddedAt = now });
        db.AuditEvents.Add(AuditEvent.Create("project.created", "Project", project.Id, user.Id, project.Id, correlation.CorrelationId, now,
            new { project.Code, project.Name, ownerId, initialBudget = cmd.InitialBudget }));
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);   // leaving the block without Commit rolls everything back, including the project row

        return await ToDtoAsync(project, ct);
    }

    /// <summary>B2. Needs the project's current Version; a stale Version raises a concurrency conflict (409).</summary>
    public async Task<ProjectDto> UpdateAsync(int id, UpdateProjectCommand cmd, CancellationToken ct)
    {
        var project = await db.Projects.FirstOrDefaultAsync(p => p.Id == id, ct) ?? throw new NotFoundException("Project", id);
        await scope.EnsureCanManageAsync(id, ct);
        await updateValidator.ValidateAndThrowAsync(cmd, ct);
        db.Entry(project).Property(p => p.Version).OriginalValue = cmd.Version;

        var now = clock.GetUtcNow();
        var before = new { project.Name, project.Status, project.OwnerId, project.StartDate, project.PlannedEndDate };

        if (cmd.OwnerId != project.OwnerId)
        {
            await EnsureProjectManagerAsync(cmd.OwnerId, ct);
            await ChangeOwnerAsync(project, cmd.OwnerId, now, ct);
        }

        project.Name = cmd.Name.Trim();
        project.Description = cmd.Description?.Trim();
        project.Status = cmd.Status;
        project.StartDate = cmd.StartDate;
        project.PlannedEndDate = cmd.PlannedEndDate;
        project.ActualEndDate = cmd.ActualEndDate;
        project.UpdatedAt = now;
        project.UpdatedById = user.Id;

        db.AuditEvents.Add(AuditEvent.Create("project.updated", "Project", id, user.Id, id, correlation.CorrelationId, now,
            new { before, after = new { project.Name, project.Status, project.OwnerId, project.StartDate, project.PlannedEndDate } }));
        await db.SaveChangesAsync(ct);   // one SaveChanges = one transaction: project, membership and audit together
        return await ToDtoAsync(project, ct);
    }

    private int ResolveOwnerId(int? requestedOwnerId)
    {
        if (user.IsAdmin)
        {
            return requestedOwnerId ?? throw new DomainRuleException("owner_required", "Choose the owning Project Manager.", isValidation: true);
        }

        if (user.IsInRole(Roles.ProjectManager))
        {
            return requestedOwnerId is null || requestedOwnerId == user.Id
                ? user.Id
                : throw new ForbiddenException("A Project Manager can only create projects they own.");
        }

        throw new ForbiddenException("Only Project Managers and Administrators can create projects.");
    }

    private async Task EnsureProjectManagerAsync(int userId, CancellationToken ct)
    {
        var owner = await users.FindAsync(userId, ct);
        if (owner is null || !owner.IsActive || owner.Role != Roles.ProjectManager)
        {
            throw new DomainRuleException("owner_not_project_manager", "The owner must be an active Project Manager.", isValidation: true);
        }
    }

    /// <summary>New owner becomes the project's manager member; the previous owner stays on as a contributor.</summary>
    private async Task ChangeOwnerAsync(Project project, int newOwnerId, DateTimeOffset now, CancellationToken ct)
    {
        var members = await db.ProjectMembers.Where(m => m.ProjectId == project.Id).ToListAsync(ct);
        var previous = members.FirstOrDefault(m => m.UserId == project.OwnerId);
        if (previous is not null)
        {
            previous.MemberRole = MemberRole.Contributor;
        }

        var next = members.FirstOrDefault(m => m.UserId == newOwnerId);
        if (next is null)
        {
            db.ProjectMembers.Add(new ProjectMember { ProjectId = project.Id, UserId = newOwnerId, MemberRole = MemberRole.Manager, AddedAt = now });
        }
        else
        {
            next.MemberRole = MemberRole.Manager;
        }

        project.OwnerId = newOwnerId;
    }

    private async Task<ProjectDto> ToDtoAsync(Project p, CancellationToken ct)
    {
        var department = await db.Departments.AsNoTracking().Where(d => d.Id == p.DepartmentId).Select(d => d.Name).FirstAsync(ct);
        var budget = await db.ProjectBudgets.AsNoTracking().Where(b => b.ProjectId == p.Id).Select(b => b.InitialAmount).FirstAsync(ct);
        var owner = (await users.NamesAsync([p.OwnerId], ct))[p.OwnerId];
        return new ProjectDto(p.Id, p.Code, p.Name, p.Description, p.DepartmentId, department, p.OwnerId, owner,
            p.Status, p.StartDate, p.PlannedEndDate, p.ActualEndDate, budget, p.Version);
    }
}
