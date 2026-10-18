using Microsoft.AspNetCore.Mvc;
using Pmo.Application.Common;
using Pmo.Application.Features.Dashboard;
using Pmo.Application.Features.Finance.Dtos;

namespace Pmo.Api.Controllers;

/// <summary>
/// Epic G. Any signed-in user: the numbers are limited to the projects the caller can see. Every route takes the same
/// filters (departmentId, projectId, managerId, status, from, to, asOf; categoryId for finance visuals).
/// </summary>
[ApiController]
[Route("api/dashboard")]
public class DashboardController(DashboardService dashboard) : ControllerBase
{
    [HttpGet("kpis")]
    [ProducesResponseType<DashboardKpisDto>(200)]
    [ProducesResponseType<ProblemDetails>(400)]
    public async Task<ActionResult<DashboardKpisDto>> Kpis([FromQuery] DashboardFilter filter, CancellationToken ct) => Ok(await dashboard.KpisAsync(filter, ct));

    [HttpGet("finance/by-project")]
    [ProducesResponseType<List<ProjectFinanceBarDto>>(200)]
    public async Task<ActionResult<List<ProjectFinanceBarDto>>> FinanceByProject([FromQuery] DashboardFilter filter, CancellationToken ct)
        => Ok(await dashboard.FinanceByProjectAsync(filter, ct));

    [HttpGet("finance/monthly")]
    [ProducesResponseType<List<MonthlyPointDto>>(200)]
    public async Task<ActionResult<List<MonthlyPointDto>>> FinanceMonthly([FromQuery] DashboardFilter filter, CancellationToken ct)
        => Ok(await dashboard.FinanceMonthlyAsync(filter, ct));

    [HttpGet("finance/by-category")]
    [ProducesResponseType<List<CategorySpendDto>>(200)]
    public async Task<ActionResult<List<CategorySpendDto>>> FinanceByCategory([FromQuery] DashboardFilter filter, CancellationToken ct)
        => Ok(await dashboard.FinanceByCategoryAsync(filter, ct));

    [HttpGet("delivery/overdue-by-owner")]
    [ProducesResponseType<List<OverdueByOwnerDto>>(200)]
    public async Task<ActionResult<List<OverdueByOwnerDto>>> OverdueByOwner([FromQuery] DashboardFilter filter, CancellationToken ct)
        => Ok(await dashboard.OverdueByOwnerAsync(filter, ct));

    [HttpGet("delivery/project-health")]
    [ProducesResponseType<List<ProjectHealthRowDto>>(200)]
    public async Task<ActionResult<List<ProjectHealthRowDto>>> ProjectHealth([FromQuery] DashboardFilter filter, CancellationToken ct)
        => Ok(await dashboard.ProjectHealthAsync(filter, ct));

    [HttpGet("risks/matrix")]
    [ProducesResponseType<List<RiskMatrixCellDto>>(200)]
    public async Task<ActionResult<List<RiskMatrixCellDto>>> RiskMatrix([FromQuery] DashboardFilter filter, CancellationToken ct)
        => Ok(await dashboard.RiskMatrixAsync(filter, ct));

    /// <summary>G4/F11. The posted transactions behind actual spend; <c>totalAmount</c> equals the KPI (without a category filter).</summary>
    [HttpGet("drilldown/costs")]
    [ProducesResponseType<DrilldownCostsResult>(200)]
    public async Task<ActionResult<DrilldownCostsResult>> DrilldownCosts(
        [FromQuery] DashboardFilter filter, [FromQuery] CostDrilldownOptions options, CancellationToken ct)
        => Ok(await dashboard.CostsDrilldownAsync(filter, options, ct));

    /// <summary>G4. The open tasks behind "overdue tasks" (overdue=true, the default) and its owner bars.</summary>
    [HttpGet("drilldown/tasks")]
    [ProducesResponseType<PagedResult<DrilldownTaskDto>>(200)]
    public async Task<ActionResult<PagedResult<DrilldownTaskDto>>> DrilldownTasks(
        [FromQuery] DashboardFilter filter, [FromQuery] TaskDrilldownOptions options, CancellationToken ct)
        => Ok(await dashboard.TasksDrilldownAsync(filter, options, ct));

    /// <summary>G4/D3. The open risks behind a matrix cell or the high-exposure KPI.</summary>
    [HttpGet("drilldown/risks")]
    [ProducesResponseType<PagedResult<DrilldownRiskDto>>(200)]
    public async Task<ActionResult<PagedResult<DrilldownRiskDto>>> DrilldownRisks(
        [FromQuery] DashboardFilter filter, [FromQuery] RiskDrilldownOptions options, CancellationToken ct)
        => Ok(await dashboard.RisksDrilldownAsync(filter, options, ct));
}
