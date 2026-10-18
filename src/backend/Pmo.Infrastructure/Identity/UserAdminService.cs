using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Pmo.Application.Common;
using Pmo.Application.Common.Abstractions;
using Pmo.Application.Common.Exceptions;
using Pmo.Application.Features.Admin;
using Pmo.Domain.Common;
using Pmo.Domain.Governance;
using Pmo.Domain.Portfolio;
using Pmo.Infrastructure.Persistence;

namespace Pmo.Infrastructure.Identity;

/// <summary>Story A4. Role and active-flag changes by an Administrator, each audited with before and after.</summary>
public sealed class UserAdminService(
    UserManager<AppUser> userManager, PmoDbContext db, IUserDirectory directory, ICurrentUser caller, TimeProvider clock,
    ICorrelationIdAccessor correlation) : IUserAdmin
{
    private static readonly string[] AllRoles = [Roles.Administrator, Roles.ProjectManager, Roles.Contributor];

    public Task<List<UserSummary>> ListAsync(CancellationToken ct) => directory.ListAsync(null, includeInactive: true, ct);

    public async Task<UserSummary> ChangeRoleAsync(int userId, string role, CancellationToken ct)
    {
        if (!AllRoles.Contains(role))
        {
            throw new DomainRuleException("invalid_role", "Role must be Administrator, ProjectManager or Contributor.", isValidation: true);
        }

        var user = await userManager.FindByIdAsync(userId.ToString()) ?? throw new NotFoundException("User", userId);
        var current = (await userManager.GetRolesAsync(user)).Single();
        if (current == role)
        {
            return (await directory.FindAsync(userId, ct))!;
        }

        if (userId == caller.Id)
        {
            throw new DomainRuleException("cannot_change_own_role", "You cannot change your own role: that could lock everyone out of administration.");
        }

        if (current == Roles.ProjectManager && await db.Projects.AnyAsync(p => p.OwnerId == userId, ct))
        {
            throw new DomainRuleException("user_owns_projects", "This Project Manager still owns projects. Hand them over first.");
        }

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await userManager.RemoveFromRoleAsync(user, current);
        await userManager.AddToRoleAsync(user, role);
        if (current == Roles.ProjectManager)
        {
            // Manager membership is only valid for Project Managers; the person stays a plain member.
            foreach (var m in await db.ProjectMembers.Where(m => m.UserId == userId && m.MemberRole == MemberRole.Manager).ToListAsync(ct))
            {
                m.MemberRole = MemberRole.Contributor;
            }
        }

        db.AuditEvents.Add(AuditEvent.Create("user.role_changed", "User", userId, caller.Id, null, correlation.CorrelationId, clock.GetUtcNow(),
            new { from = current, to = role }));
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return (await directory.FindAsync(userId, ct))!;
    }

    public async Task<UserSummary> SetActiveAsync(int userId, bool isActive, CancellationToken ct)
    {
        var user = await userManager.FindByIdAsync(userId.ToString()) ?? throw new NotFoundException("User", userId);
        if (!isActive && userId == caller.Id)
        {
            throw new DomainRuleException("cannot_deactivate_self", "You cannot deactivate your own account.");
        }

        if (user.IsActive != isActive)
        {
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            user.IsActive = isActive;
            await userManager.UpdateAsync(user);
            if (!isActive)
            {
                db.UserTokens.RemoveRange(db.UserTokens.Where(t => t.UserId == userId));   // sign them out: refresh tokens are revoked
            }

            db.AuditEvents.Add(AuditEvent.Create("user.status_changed", "User", userId, caller.Id, null, correlation.CorrelationId, clock.GetUtcNow(),
                new { isActive }));
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        }

        return (await directory.FindAsync(userId, ct))!;
    }
}
