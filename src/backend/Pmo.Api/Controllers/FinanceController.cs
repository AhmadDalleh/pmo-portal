using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pmo.Api.Authorization;
using Pmo.Application.Features.Finance;
using Pmo.Application.Features.Finance.Commands;
using Pmo.Application.Features.Finance.Dtos;

namespace Pmo.Api.Controllers;

/// <summary>Read side of finance (F1, F9), forecasts (F8), budget revisions (F3, F4) and categories (F2).</summary>
[ApiController]
public class FinanceController(
    FinanceQueryService queries, ForecastService forecasts, BudgetRevisionService revisions, CostCategoryService categories) : ControllerBase
{
    [HttpGet("api/projects/{projectId:int}/finance/summary")]
    [ProducesResponseType<FinanceSummaryDto>(200)]
    [ProducesResponseType<ProblemDetails>(404)]
    public async Task<ActionResult<FinanceSummaryDto>> Summary(int projectId, [FromQuery] DateOnly? asOf, CancellationToken ct)
        => Ok(await queries.SummaryAsync(projectId, asOf, ct));

    [HttpGet("api/projects/{projectId:int}/finance/by-category")]
    [ProducesResponseType<List<CategorySpendDto>>(200)]
    public async Task<ActionResult<List<CategorySpendDto>>> ByCategory(int projectId, [FromQuery] DateOnly? asOf, CancellationToken ct)
        => Ok(await queries.ByCategoryAsync(projectId, asOf, ct));

    [HttpGet("api/projects/{projectId:int}/finance/monthly")]
    [ProducesResponseType<List<MonthlyPointDto>>(200)]
    public async Task<ActionResult<List<MonthlyPointDto>>> Monthly(
        int projectId, [FromQuery] DateOnly? asOf, [FromQuery] int? categoryId, CancellationToken ct)
        => Ok(await queries.MonthlyAsync(projectId, asOf, categoryId, ct));

    [HttpGet("api/projects/{projectId:int}/forecasts")]
    [ProducesResponseType<List<ForecastDto>>(200)]
    public async Task<ActionResult<List<ForecastDto>>> GetForecasts(int projectId, CancellationToken ct)
        => Ok(await forecasts.ListAsync(projectId, ct));

    [HttpPut("api/projects/{projectId:int}/forecasts")]
    [Authorize(Policy = Policies.ManageProjects)]
    [ProducesResponseType<List<ForecastDto>>(200)]
    [ProducesResponseType<ProblemDetails>(400)]
    [ProducesResponseType<ProblemDetails>(403)]
    [ProducesResponseType<ProblemDetails>(404)]
    public async Task<ActionResult<List<ForecastDto>>> SaveForecasts(int projectId, SaveForecastsCommand body, CancellationToken ct)
        => Ok(await forecasts.SaveAsync(projectId, body, ct));

    [HttpGet("api/projects/{projectId:int}/budget-revisions")]
    [ProducesResponseType<List<BudgetRevisionDto>>(200)]
    public async Task<ActionResult<List<BudgetRevisionDto>>> ListRevisions(int projectId, CancellationToken ct)
        => Ok(await revisions.ListAsync(projectId, ct));

    [HttpPost("api/projects/{projectId:int}/budget-revisions")]
    [Authorize(Policy = Policies.ManageProjects)]
    [ProducesResponseType<BudgetRevisionDto>(201)]
    [ProducesResponseType<ProblemDetails>(400)]
    [ProducesResponseType<ProblemDetails>(403)]
    [ProducesResponseType<ProblemDetails>(404)]
    public async Task<ActionResult<BudgetRevisionDto>> RequestRevision(int projectId, RequestRevisionCommand body, CancellationToken ct)
    {
        var created = await revisions.RequestAsync(projectId, body, ct);
        return Created($"/api/projects/{projectId}/budget-revisions", created);
    }

    /// <summary>F4. Administrator only; never the person who requested it.</summary>
    [HttpPost("api/finance/budget-revisions/{revisionId:int}/decision")]
    [Authorize(Policy = Policies.AdminOnly)]
    [ProducesResponseType<BudgetRevisionDto>(200)]
    [ProducesResponseType<ProblemDetails>(400)]
    [ProducesResponseType<ProblemDetails>(403)]
    [ProducesResponseType<ProblemDetails>(404)]
    [ProducesResponseType<ProblemDetails>(409)]
    public async Task<ActionResult<BudgetRevisionDto>> Decide(int revisionId, DecideRevisionCommand body, CancellationToken ct)
        => Ok(await revisions.DecideAsync(revisionId, body, ct));

    [HttpGet("api/finance/cost-categories")]
    [ProducesResponseType<List<CostCategoryDto>>(200)]
    public async Task<ActionResult<List<CostCategoryDto>>> ListCategories(CancellationToken ct) => Ok(await categories.ListAsync(ct));

    [HttpPost("api/finance/cost-categories")]
    [Authorize(Policy = Policies.AdminOnly)]
    [ProducesResponseType<CostCategoryDto>(201)]
    [ProducesResponseType<ProblemDetails>(400)]
    [ProducesResponseType<ProblemDetails>(409)]
    public async Task<ActionResult<CostCategoryDto>> CreateCategory(CreateCategoryCommand body, CancellationToken ct)
    {
        var created = await categories.CreateAsync(body, ct);
        return Created("/api/finance/cost-categories", created);
    }

    [HttpPut("api/finance/cost-categories/{id:int}")]
    [Authorize(Policy = Policies.AdminOnly)]
    [ProducesResponseType<CostCategoryDto>(200)]
    [ProducesResponseType<ProblemDetails>(404)]
    [ProducesResponseType<ProblemDetails>(409)]
    public async Task<ActionResult<CostCategoryDto>> UpdateCategory(int id, UpdateCategoryCommand body, CancellationToken ct)
        => Ok(await categories.UpdateAsync(id, body, ct));
}
