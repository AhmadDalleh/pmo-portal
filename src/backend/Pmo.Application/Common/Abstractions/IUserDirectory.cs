namespace Pmo.Application.Common.Abstractions;

/// <summary>Reads the Identity user table (which lives in Infrastructure) for services that only need names and roles.</summary>
public interface IUserDirectory
{
    Task<UserSummary?> FindAsync(int userId, CancellationToken cancellationToken);

    /// <summary>Full names by user id, for building DTOs without joining Identity tables in every query.</summary>
    Task<Dictionary<int, string>> NamesAsync(IEnumerable<int> userIds, CancellationToken cancellationToken);

    /// <summary>Users ordered by name, optionally limited to one role. Inactive users are left out unless asked for.</summary>
    Task<List<UserSummary>> ListAsync(string? role, bool includeInactive, CancellationToken cancellationToken);
}
