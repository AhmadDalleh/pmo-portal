using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pmo.Api.Authorization;
using Pmo.Application.Common;
using Pmo.Application.Features.Finance;
using Pmo.Application.Features.Finance.Commands;
using Pmo.Application.Features.Finance.Dtos;
using Pmo.Application.Features.Finance.Queries;

namespace Pmo.Api.Controllers;

/// <summary>Cost transactions (F5, F6, F7). Writes need the CanPostCosts role policy AND management of the project.</summary>
[ApiController]
public class CostsController(CostService costs) : ControllerBase
{
    [HttpGet("api/projects/{projectId:int}/costs")]
    [ProducesResponseType<PagedResult<CostDto>>(200)]
    [ProducesResponseType<ProblemDetails>(404)]
    public async Task<ActionResult<PagedResult<CostDto>>> List(int projectId, [FromQuery] CostSearchQuery query, CancellationToken ct)
        => Ok(await costs.ListAsync(projectId, query, ct));

    [HttpGet("api/finance/costs/{costId:long}")]
    [ProducesResponseType<CostDto>(200)]
    [ProducesResponseType<ProblemDetails>(404)]
    public async Task<ActionResult<CostDto>> Get(long costId, CancellationToken ct) => Ok(await costs.GetAsync(costId, ct));

    /// <summary>201 for a new draft, 200 with the original when the same Idempotency-Key is sent again.</summary>
    [HttpPost("api/projects/{projectId:int}/costs")]
    [Authorize(Policy = Policies.CanPostCosts)]
    [ProducesResponseType<CostDto>(201)]
    [ProducesResponseType<CostDto>(200)]
    [ProducesResponseType<ProblemDetails>(400)]
    [ProducesResponseType<ProblemDetails>(403)]
    [ProducesResponseType<ProblemDetails>(404)]
    [ProducesResponseType<ProblemDetails>(409)]
    public async Task<ActionResult<CostDto>> Create(
        int projectId, CreateCostCommand body, [FromHeader(Name = "Idempotency-Key")] Guid? idempotencyKey, CancellationToken ct)
    {
        var (cost, created) = await costs.CreateDraftAsync(projectId, idempotencyKey, body, ct);
        return created ? CreatedAtAction(nameof(Get), new { costId = cost.Id }, cost) : Ok(cost);
    }

    [HttpPut("api/finance/costs/{costId:long}")]
    [Authorize(Policy = Policies.CanPostCosts)]
    [ProducesResponseType<CostDto>(200)]
    [ProducesResponseType<ProblemDetails>(400)]
    [ProducesResponseType<ProblemDetails>(403)]
    [ProducesResponseType<ProblemDetails>(404)]
    [ProducesResponseType<ProblemDetails>(409)]
    public async Task<ActionResult<CostDto>> Update(long costId, UpdateCostCommand body, CancellationToken ct)
        => Ok(await costs.UpdateDraftAsync(costId, body, ct));

    [HttpDelete("api/finance/costs/{costId:long}")]
    [Authorize(Policy = Policies.CanPostCosts)]
    [ProducesResponseType(204)]
    [ProducesResponseType<ProblemDetails>(403)]
    [ProducesResponseType<ProblemDetails>(404)]
    [ProducesResponseType<ProblemDetails>(409)]
    public async Task<IActionResult> Delete(long costId, CancellationToken ct)
    {
        await costs.DeleteDraftAsync(costId, ct);
        return NoContent();
    }

    [HttpPost("api/finance/costs/{costId:long}/post")]
    [Authorize(Policy = Policies.CanPostCosts)]
    [ProducesResponseType<CostDto>(200)]
    [ProducesResponseType<ProblemDetails>(403)]
    [ProducesResponseType<ProblemDetails>(404)]
    [ProducesResponseType<ProblemDetails>(409)]
    public async Task<ActionResult<CostDto>> Post(long costId, PostCostCommand body, CancellationToken ct)
        => Ok(await costs.PostAsync(costId, body, ct));

    [HttpPost("api/finance/costs/{costId:long}/reverse")]
    [Authorize(Policy = Policies.CanPostCosts)]
    [ProducesResponseType<CostDto>(201)]
    [ProducesResponseType<ProblemDetails>(400)]
    [ProducesResponseType<ProblemDetails>(403)]
    [ProducesResponseType<ProblemDetails>(404)]
    [ProducesResponseType<ProblemDetails>(409)]
    public async Task<ActionResult<CostDto>> Reverse(long costId, ReverseCostCommand body, CancellationToken ct)
    {
        var reversal = await costs.ReverseAsync(costId, body, ct);
        return CreatedAtAction(nameof(Get), new { costId = reversal.Id }, reversal);
    }
}
