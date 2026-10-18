namespace Pmo.Application.Common.Abstractions;

/// <summary>
/// "Which projects may the caller see, and which may they manage?" Administrators see everything;
/// everyone else sees the projects they are a member of. Out of scope => 404 (hides existence);
/// in scope but not allowed => 403.
/// </summary>
public interface IProjectScope
{
    /// <summary>Ids of the projects the caller may see.</summary>
    Task<int[]> AccessibleProjectIdsAsync(CancellationToken cancellationToken);

    /// <summary>Ids of the projects the caller may manage (Administrator: all; Project Manager: member as manager).</summary>
    Task<int[]> ManagedProjectIdsAsync(CancellationToken cancellationToken);

    /// <summary>Throws NotFoundException when the project is outside the caller's scope.</summary>
    Task EnsureCanViewAsync(int projectId, CancellationToken cancellationToken);

    /// <summary>Throws NotFoundException when out of scope, ForbiddenException when visible but not manageable.</summary>
    Task EnsureCanManageAsync(int projectId, CancellationToken cancellationToken);

    Task<bool> CanManageAsync(int projectId, CancellationToken cancellationToken);
}
