using Microsoft.EntityFrameworkCore;
using Pmo.Application.Common.Abstractions;
using Pmo.Domain.Portfolio;

namespace Pmo.Application.Common;

/// <summary>The "which projects can this user manage?" rule, usable before a request is authenticated (sign-in builds /me with it).</summary>
public static class ProjectAccess
{
    /// <summary>Administrator: every project. Project Manager: projects where they are a manager member. Contributor: none.</summary>
    public static async Task<int[]> ManagedIdsAsync(IPmoDbContext db, int userId, string role, CancellationToken cancellationToken)
    {
        if (role == Roles.Administrator)
        {
            return await db.Projects.AsNoTracking().Select(p => p.Id).ToArrayAsync(cancellationToken);
        }

        if (role == Roles.ProjectManager)
        {
            return await db.ProjectMembers.AsNoTracking()
                .Where(m => m.UserId == userId && m.MemberRole == MemberRole.Manager)
                .Select(m => m.ProjectId).ToArrayAsync(cancellationToken);
        }

        return [];
    }
}
