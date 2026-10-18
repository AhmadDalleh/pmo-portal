using Pmo.Application.Common.Abstractions;

namespace Pmo.Application.Features.Admin;

/// <summary>Changes that need ASP.NET Core Identity (role, active flag), implemented in Infrastructure. Administrator routes only.</summary>
public interface IUserAdmin
{
    Task<List<UserSummary>> ListAsync(CancellationToken cancellationToken);

    /// <summary>Not your own role (409), and not away from Project Manager while the user still owns projects (409).</summary>
    Task<UserSummary> ChangeRoleAsync(int userId, string role, CancellationToken cancellationToken);

    Task<UserSummary> SetActiveAsync(int userId, bool isActive, CancellationToken cancellationToken);
}
