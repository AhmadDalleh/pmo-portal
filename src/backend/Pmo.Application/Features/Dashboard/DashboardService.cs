using FluentValidation;
using Microsoft.Extensions.Options;
using Pmo.Application.Common;
using Pmo.Application.Common.Abstractions;
using Pmo.Application.Features.Finance.Dtos;
using Pmo.Application.Features.Projects;
using Pmo.Application.Features.Projects.Queries;
using Pmo.Domain.Portfolio;

namespace Pmo.Application.Features.Dashboard;

/// <summary>
/// Epic G. All numbers are computed by SQL. The service only (1) resolves the caller's scope, so a filter can never reach
/// a project the caller cannot see, and (2) resolves the as-of date.
/// </summary>
public sealed class DashboardService(
    IProjectScope scope, IDashboardReadModel read, IProjectReadModel projects, IOptions<ReportingOptions> reporting,
    IValidator<DashboardFilter> filterValidator)
{
    public async Task<DashboardKpisDto> KpisAsync(DashboardFilter filter, CancellationToken ct)
    {
        var (ids, asOf) = await PrepareAsync(filter, ct);
        return await read.KpisAsync(filter, ids, asOf, ct);
    }

    public async Task<List<ProjectFinanceBarDto>> FinanceByProjectAsync(DashboardFilter filter, CancellationToken ct)
    {
        var (ids, asOf) = await PrepareAsync(filter, ct);
        return await read.FinanceByProjectAsync(filter, ids, asOf, ct);
    }

    public async Task<List<MonthlyPointDto>> FinanceMonthlyAsync(DashboardFilter filter, CancellationToken ct)
    {
        var (ids, asOf) = await PrepareAsync(filter, ct);
        return await read.FinanceMonthlyAsync(filter, ids, asOf, ct);
    }

    public async Task<List<CategorySpendDto>> FinanceByCategoryAsync(DashboardFilter filter, CancellationToken ct)
    {
        var (ids, asOf) = await PrepareAsync(filter, ct);
        return await read.FinanceByCategoryAsync(filter, ids, asOf, ct);
    }

    public async Task<List<OverdueByOwnerDto>> OverdueByOwnerAsync(DashboardFilter filter, CancellationToken ct)
    {
        var (ids, asOf) = await PrepareAsync(filter, ct);
        return await read.OverdueByOwnerAsync(filter, ids, asOf, ct);
    }

    public async Task<List<RiskMatrixCellDto>> RiskMatrixAsync(DashboardFilter filter, CancellationToken ct)
    {
        var (ids, asOf) = await PrepareAsync(filter, ct);
        return await read.RiskMatrixAsync(filter, ids, asOf, ct);
    }

    /// <summary>
    /// Visual 6. Reuses the portfolio list query (same filters, same KPI definitions) and the health rule of story B4,
    /// so a project has the same badge here and on the project page.
    /// </summary>
    public async Task<List<ProjectHealthRowDto>> ProjectHealthAsync(DashboardFilter filter, CancellationToken ct)
    {
        var (ids, asOf) = await PrepareAsync(filter, ct);
        var query = new ProjectSearchQuery(
            DepartmentId: filter.DepartmentId, OwnerId: filter.ManagerId, Status: filter.Status, From: filter.From, To: filter.To, PageSize: 100);
        var scopeIds = filter.ProjectId is { } only ? ids.Where(id => id == only).ToArray() : ids;

        var rows = await projects.SearchAsync(query, scopeIds, asOf, ct);
        return rows.Select(r => new ProjectHealthRowDto(
            r.Id, r.Code, r.Name, r.Status, r.ProgressPct, r.OverdueTasks, r.OpenHighRisks, r.Utilization, ProjectHealth.Evaluate(r))).ToList();
    }

    public async Task<DrilldownCostsResult> CostsDrilldownAsync(DashboardFilter filter, CostDrilldownOptions options, CancellationToken ct)
    {
        var (ids, asOf) = await PrepareAsync(filter, ct);
        return await read.CostsDrilldownAsync(filter, ids, asOf, Paged(options), ct);
    }

    public async Task<PagedResult<DrilldownTaskDto>> TasksDrilldownAsync(DashboardFilter filter, TaskDrilldownOptions options, CancellationToken ct)
    {
        var (ids, asOf) = await PrepareAsync(filter, ct);
        return await read.TasksDrilldownAsync(filter, ids, asOf, options with { Page = Math.Max(1, options.Page), PageSize = Math.Clamp(options.PageSize, 1, 100) }, ct);
    }

    public async Task<PagedResult<DrilldownRiskDto>> RisksDrilldownAsync(DashboardFilter filter, RiskDrilldownOptions options, CancellationToken ct)
    {
        var (ids, asOf) = await PrepareAsync(filter, ct);
        return await read.RisksDrilldownAsync(filter, ids, asOf, options with { Page = Math.Max(1, options.Page), PageSize = Math.Clamp(options.PageSize, 1, 100) }, ct);
    }

    private async Task<(int[] ScopeIds, DateOnly AsOf)> PrepareAsync(DashboardFilter filter, CancellationToken ct)
    {
        await filterValidator.ValidateAndThrowAsync(filter, ct);
        return (await scope.AccessibleProjectIdsAsync(ct), reporting.Value.Resolve(filter.AsOf));
    }

    private static CostDrilldownOptions Paged(CostDrilldownOptions o) => o with { Page = Math.Max(1, o.Page), PageSize = Math.Clamp(o.PageSize, 1, 100) };
}
