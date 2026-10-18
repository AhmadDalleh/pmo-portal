using Pmo.Application.Features.Finance.Dtos;

namespace Pmo.Application.Features.Finance;

/// <summary>Finance charts for one project, written as SQL in Infrastructure (database-design.md sections 6.1 and 6.2).</summary>
public interface IFinanceReadModel
{
    Task<List<CategorySpendDto>> ByCategoryAsync(int projectId, DateOnly asOf, CancellationToken cancellationToken);

    Task<List<MonthlyPointDto>> MonthlyAsync(int projectId, DateOnly asOf, int? categoryId, CancellationToken cancellationToken);
}
