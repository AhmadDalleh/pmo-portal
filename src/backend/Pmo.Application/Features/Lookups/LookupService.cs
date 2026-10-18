using Microsoft.EntityFrameworkCore;
using Pmo.Application.Common.Abstractions;

namespace Pmo.Application.Features.Lookups;

/// <summary>Story B6: small lists for dropdowns. Any signed-in user may read them.</summary>
public sealed class LookupService(IPmoDbContext db, IUserDirectory users)
{
    public Task<List<LookupItem>> DepartmentsAsync(CancellationToken ct) =>
        db.Departments.AsNoTracking().Where(d => d.IsActive).OrderBy(d => d.Name)
            .Select(d => new LookupItem(d.Id, d.Code, d.Name)).ToListAsync(ct);

    public Task<List<LookupItem>> CostCategoriesAsync(CancellationToken ct) =>
        db.CostCategories.AsNoTracking().Where(c => c.IsActive).OrderBy(c => c.Name)
            .Select(c => new LookupItem(c.Id, c.Code, c.Name)).ToListAsync(ct);

    public async Task<List<UserLookupItem>> UsersAsync(string? role, CancellationToken ct) =>
        (await users.ListAsync(role, includeInactive: false, ct))
        .Select(u => new UserLookupItem(u.Id, u.FullName, u.Email, u.Role, u.DepartmentId)).ToList();
}
