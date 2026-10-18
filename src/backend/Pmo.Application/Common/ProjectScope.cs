using Microsoft.EntityFrameworkCore;
using Pmo.Application.Common.Abstractions;
using Pmo.Application.Common.Exceptions;
using Pmo.Domain.Portfolio;

namespace Pmo.Application.Common;

/// <summary>
/// Scope rules of the permission matrix (user-stories.md). Registered per request, so each list is read once per request.
/// </summary>
public sealed class ProjectScope(IPmoDbContext db, ICurrentUser user) : IProjectScope
{
    private int[]? _accessible;
    private int[]? _managed;

    public async Task<int[]> AccessibleProjectIdsAsync(CancellationToken cancellationToken)
    {
        if (_accessible is not null)
        {
            return _accessible;
        }

        _accessible = user.IsAdmin
            ? await db.Projects.AsNoTracking().Select(p => p.Id).ToArrayAsync(cancellationToken)
            : await db.ProjectMembers.AsNoTracking().Where(m => m.UserId == user.Id).Select(m => m.ProjectId).ToArrayAsync(cancellationToken);
        return _accessible;
    }

    public async Task<int[]> ManagedProjectIdsAsync(CancellationToken cancellationToken)
    {
        if (_managed is not null)
        {
            return _managed;
        }

        _managed = await ProjectAccess.ManagedIdsAsync(db, user.Id, user.Role, cancellationToken);
        return _managed;
    }

    public async Task EnsureCanViewAsync(int projectId, CancellationToken cancellationToken)
    {
        if (!(await AccessibleProjectIdsAsync(cancellationToken)).Contains(projectId))
        {
            throw new NotFoundException("Project", projectId);
        }
    }

    public async Task EnsureCanManageAsync(int projectId, CancellationToken cancellationToken)
    {
        await EnsureCanViewAsync(projectId, cancellationToken);
        if (!await CanManageAsync(projectId, cancellationToken))
        {
            throw new ForbiddenException("You do not manage this project.");
        }
    }

    public async Task<bool> CanManageAsync(int projectId, CancellationToken cancellationToken) =>
        (await ManagedProjectIdsAsync(cancellationToken)).Contains(projectId);
}
