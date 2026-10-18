using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pmo.Api.Authorization;
using Pmo.Application.Common;
using Pmo.Application.Features.Risks;
using Pmo.Application.Features.Risks.Commands;
using Pmo.Application.Features.Risks.Dtos;
using Pmo.Application.Features.Risks.Queries;

namespace Pmo.Api.Controllers;

[ApiController]
public class RisksController(RiskService risks) : ControllerBase
{
    /// <summary>D2. Open items across the caller's projects; highExposure=true keeps score 15+; closed items never show as open.</summary>
    [HttpGet("api/risks")]
    [ProducesResponseType<PagedResult<RiskDto>>(200)]
    [ProducesResponseType<ProblemDetails>(400)]
    public async Task<ActionResult<PagedResult<RiskDto>>> Search([FromQuery] RiskSearchQuery query, CancellationToken ct)
        => Ok(await risks.SearchAsync(query, ct));

    [HttpGet("api/projects/{projectId:int}/risks")]
    [ProducesResponseType<List<RiskDto>>(200)]
    [ProducesResponseType<ProblemDetails>(404)]
    public async Task<ActionResult<List<RiskDto>>> List(int projectId, CancellationToken ct) => Ok(await risks.ListForProjectAsync(projectId, ct));

    [HttpPost("api/projects/{projectId:int}/risks")]
    [Authorize(Policy = Policies.ManageProjects)]
    [ProducesResponseType<RiskDto>(201)]
    [ProducesResponseType<ProblemDetails>(400)]
    [ProducesResponseType<ProblemDetails>(403)]
    [ProducesResponseType<ProblemDetails>(404)]
    public async Task<ActionResult<RiskDto>> Create(int projectId, CreateRiskCommand body, CancellationToken ct)
    {
        var created = await risks.CreateAsync(projectId, body, ct);
        return Created($"/api/projects/{projectId}/risks", created);
    }

    [HttpPut("api/risks/{riskId:int}")]
    [Authorize(Policy = Policies.ManageProjects)]
    [ProducesResponseType<RiskDto>(200)]
    [ProducesResponseType<ProblemDetails>(400)]
    [ProducesResponseType<ProblemDetails>(403)]
    [ProducesResponseType<ProblemDetails>(404)]
    [ProducesResponseType<ProblemDetails>(409)]
    public async Task<ActionResult<RiskDto>> Update(int riskId, UpdateRiskCommand body, CancellationToken ct)
        => Ok(await risks.UpdateAsync(riskId, body, ct));
}
