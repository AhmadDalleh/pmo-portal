using Pmo.Application.Common;
using Pmo.Application.Features.Finance.Dtos;

namespace Pmo.Application.Features.Dashboard;

/// <summary>
/// The dashboard queries (SQL in Infrastructure). Every method receives the caller's scope ids and the as-of date resolved by the
/// service; the SQL starts from one shared filtered-projects CTE so KPIs, charts and drill-downs agree.
/// </summary>
public interface IDashboardReadModel
{
    Task<DashboardKpisDto> KpisAsync(DashboardFilter filter, int[] scopeIds, DateOnly asOf, CancellationToken cancellationToken);

    Task<List<ProjectFinanceBarDto>> FinanceByProjectAsync(DashboardFilter filter, int[] scopeIds, DateOnly asOf, CancellationToken cancellationToken);

    Task<List<MonthlyPointDto>> FinanceMonthlyAsync(DashboardFilter filter, int[] scopeIds, DateOnly asOf, CancellationToken cancellationToken);

    Task<List<CategorySpendDto>> FinanceByCategoryAsync(DashboardFilter filter, int[] scopeIds, DateOnly asOf, CancellationToken cancellationToken);

    Task<List<OverdueByOwnerDto>> OverdueByOwnerAsync(DashboardFilter filter, int[] scopeIds, DateOnly asOf, CancellationToken cancellationToken);

    Task<List<RiskMatrixCellDto>> RiskMatrixAsync(DashboardFilter filter, int[] scopeIds, DateOnly asOf, CancellationToken cancellationToken);

    Task<DrilldownCostsResult> CostsDrilldownAsync(DashboardFilter filter, int[] scopeIds, DateOnly asOf, CostDrilldownOptions options, CancellationToken cancellationToken);

    Task<PagedResult<DrilldownTaskDto>> TasksDrilldownAsync(DashboardFilter filter, int[] scopeIds, DateOnly asOf, TaskDrilldownOptions options, CancellationToken cancellationToken);

    Task<PagedResult<DrilldownRiskDto>> RisksDrilldownAsync(DashboardFilter filter, int[] scopeIds, DateOnly asOf, RiskDrilldownOptions options, CancellationToken cancellationToken);
}
