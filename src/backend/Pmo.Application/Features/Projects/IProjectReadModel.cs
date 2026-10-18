using Pmo.Application.Features.Projects.Dtos;
using Pmo.Application.Features.Projects.Queries;

namespace Pmo.Application.Features.Projects;

/// <summary>Read side of the portfolio: KPI queries written as SQL in Infrastructure (database-design.md §6).</summary>
public interface IProjectReadModel
{
    /// <summary>Projects in <paramref name="scopeIds"/> that match the filters, sorted and paged.</summary>
    Task<List<ProjectKpiRow>> SearchAsync(ProjectSearchQuery query, int[] scopeIds, DateOnly asOf, CancellationToken cancellationToken);

    Task<ProjectKpiRow?> GetKpisAsync(int projectId, DateOnly asOf, CancellationToken cancellationToken);

    /// <summary>The pending milestone with the earliest due date (may already be overdue), or null.</summary>
    Task<NextMilestoneDto?> NextMilestoneAsync(int projectId, DateOnly asOf, CancellationToken cancellationToken);
}
