using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Pmo.Application.Common;
using Pmo.Application.Common.Abstractions;
using Pmo.Application.Common.Exceptions;
using Pmo.Application.Features.Projects.Commands;
using Pmo.Application.Features.Projects.Dtos;
using Pmo.Domain.Common;
using Pmo.Domain.Delivery;
using Pmo.Domain.Governance;
using Pmo.Domain.Portfolio;

namespace Pmo.Application.Features.Projects;

/// <summary>Story B5: who is on a project. Membership is what grants scope, so every change is audited.</summary>
public sealed class ProjectMemberService(
    IPmoDbContext db, ICurrentUser user, IProjectScope scope, IUserDirectory users, TimeProvider clock,
    ICorrelationIdAccessor correlation, IValidator<AddMemberCommand> addValidator)
{
    public async Task<List<MemberDto>> ListAsync(int projectId, CancellationToken ct)
    {
        await scope.EnsureCanViewAsync(projectId, ct);
        var members = await db.ProjectMembers.AsNoTracking().Where(m => m.ProjectId == projectId).ToListAsync(ct);
        var people = (await users.ListAsync(null, includeInactive: true, ct)).ToDictionary(u => u.Id);

        return members
            .Where(m => people.ContainsKey(m.UserId))
            .Select(m => new MemberDto(m.UserId, people[m.UserId].FullName, people[m.UserId].Email, people[m.UserId].Role, m.MemberRole, m.AddedAt))
            .OrderByDescending(m => m.MemberRole == MemberRole.Manager).ThenBy(m => m.FullName)
            .ToList();
    }

    public async Task<MemberDto> AddAsync(int projectId, AddMemberCommand cmd, CancellationToken ct)
    {
        await scope.EnsureCanManageAsync(projectId, ct);
        await addValidator.ValidateAndThrowAsync(cmd, ct);

        var person = await users.FindAsync(cmd.UserId, ct);
        if (person is null || !person.IsActive || person.Role == Roles.Administrator)
        {
            throw new DomainRuleException("invalid_member", "Choose an active Project Manager or Contributor.", isValidation: true);
        }

        if (cmd.MemberRole == MemberRole.Manager && person.Role != Roles.ProjectManager)
        {
            throw new DomainRuleException("manager_must_be_pm", "Only a Project Manager can be a manager member.", isValidation: true);
        }

        if (await db.ProjectMembers.AnyAsync(m => m.ProjectId == projectId && m.UserId == cmd.UserId, ct))
        {
            throw new DomainRuleException("already_member", "This user is already a member of the project.");
        }

        var now = clock.GetUtcNow();
        var member = new ProjectMember { ProjectId = projectId, UserId = cmd.UserId, MemberRole = cmd.MemberRole, AddedAt = now };
        db.ProjectMembers.Add(member);
        db.AuditEvents.Add(AuditEvent.Create("project.member_added", "Project", projectId, user.Id, projectId, correlation.CorrelationId, now,
            new { cmd.UserId, cmd.MemberRole }));
        await db.SaveChangesAsync(ct);

        return new MemberDto(person.Id, person.FullName, person.Email, person.Role, member.MemberRole, member.AddedAt);
    }

    public async Task RemoveAsync(int projectId, int userId, CancellationToken ct)
    {
        await scope.EnsureCanManageAsync(projectId, ct);
        var project = await db.Projects.AsNoTracking().FirstAsync(p => p.Id == projectId, ct);
        var member = await db.ProjectMembers.FirstOrDefaultAsync(m => m.ProjectId == projectId && m.UserId == userId, ct)
                     ?? throw new NotFoundException("Member", userId);

        if (project.OwnerId == userId)
        {
            throw new DomainRuleException("cannot_remove_owner", "The project owner cannot be removed. Change the owner first.");
        }

        var openTasks = await db.Tasks.AsNoTracking()
            .Where(t => t.ProjectId == projectId && t.AssigneeId == userId
                        && t.Status != ProjectTaskStatus.Done && t.Status != ProjectTaskStatus.Cancelled)
            .OrderBy(t => t.Id).Select(t => "#" + t.Id + " " + t.Title).ToListAsync(ct);
        if (openTasks.Count > 0)
        {
            throw new DomainRuleException("member_has_open_tasks",
                $"Reassign these open tasks first: {string.Join("; ", openTasks)}.");
        }

        var now = clock.GetUtcNow();
        db.ProjectMembers.Remove(member);
        db.AuditEvents.Add(AuditEvent.Create("project.member_removed", "Project", projectId, user.Id, projectId, correlation.CorrelationId, now,
            new { userId }));
        await db.SaveChangesAsync(ct);
    }
}
