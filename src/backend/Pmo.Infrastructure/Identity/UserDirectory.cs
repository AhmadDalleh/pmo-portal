using Microsoft.EntityFrameworkCore;
using Pmo.Application.Common.Abstractions;
using Pmo.Infrastructure.Persistence;

namespace Pmo.Infrastructure.Identity;

/// <summary>Reads users with their single role (users + user_roles + roles). Read-only.</summary>
public sealed class UserDirectory(PmoDbContext db) : IUserDirectory
{
    // Join first, filter on the joined columns, project last: EF can translate that shape.
    private IQueryable<UserWithRole> UsersWithRole() =>
        from u in db.Users.AsNoTracking()
        join ur in db.UserRoles on u.Id equals ur.UserId
        join r in db.Roles on ur.RoleId equals r.Id
        select new UserWithRole { User = u, RoleName = r.Name! };

    public async Task<UserSummary?> FindAsync(int userId, CancellationToken cancellationToken)
    {
        var row = await UsersWithRole().Where(x => x.User.Id == userId).FirstOrDefaultAsync(cancellationToken);
        return row is null ? null : ToSummary(row);
    }

    public async Task<Dictionary<int, string>> NamesAsync(IEnumerable<int> userIds, CancellationToken cancellationToken)
    {
        var ids = userIds.Distinct().ToArray();
        return await db.Users.AsNoTracking().Where(u => ids.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.FullName, cancellationToken);
    }

    public async Task<List<UserSummary>> ListAsync(string? role, bool includeInactive, CancellationToken cancellationToken)
    {
        var rows = await UsersWithRole()
            .Where(x => (includeInactive || x.User.IsActive) && (role == null || x.RoleName == role))
            .OrderBy(x => x.User.FullName).ToListAsync(cancellationToken);
        return rows.Select(ToSummary).ToList();
    }

    private static UserSummary ToSummary(UserWithRole x) =>
        new(x.User.Id, x.User.Email!, x.User.FullName, x.User.DepartmentId, x.User.JobTitle, x.RoleName, x.User.IsActive);

    private sealed class UserWithRole
    {
        public required AppUser User { get; init; }
        public required string RoleName { get; init; }
    }
}
