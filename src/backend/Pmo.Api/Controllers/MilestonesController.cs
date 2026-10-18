using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pmo.Api.Authorization;
using Pmo.Application.Features.Milestones;
using Pmo.Application.Features.Milestones.Commands;
using Pmo.Application.Features.Milestones.Dtos;

namespace Pmo.Api.Controllers;

[ApiController]
public class MilestonesController(MilestoneService milestones) : ControllerBase
{
    [HttpGet("api/projects/{projectId:int}/milestones")]
    [ProducesResponseType<List<MilestoneDto>>(200)]
    [ProducesResponseType<ProblemDetails>(404)]
    public async Task<ActionResult<List<MilestoneDto>>> List(int projectId, [FromQuery] DateOnly? asOf, CancellationToken ct)
        => Ok(await milestones.ListAsync(projectId, asOf, ct));

    [HttpPost("api/projects/{projectId:int}/milestones")]
    [Authorize(Policy = Policies.ManageProjects)]
    [ProducesResponseType<MilestoneDto>(201)]
    [ProducesResponseType<ProblemDetails>(400)]
    [ProducesResponseType<ProblemDetails>(403)]
    [ProducesResponseType<ProblemDetails>(404)]
    public async Task<ActionResult<MilestoneDto>> Create(int projectId, CreateMilestoneCommand body, CancellationToken ct)
    {
        var created = await milestones.CreateAsync(projectId, body, ct);
        return Created($"/api/projects/{projectId}/milestones", created);
    }

    [HttpPut("api/milestones/{milestoneId:int}")]
    [Authorize(Policy = Policies.ManageProjects)]
    [ProducesResponseType<MilestoneDto>(200)]
    [ProducesResponseType<ProblemDetails>(400)]
    [ProducesResponseType<ProblemDetails>(403)]
    [ProducesResponseType<ProblemDetails>(404)]
    [ProducesResponseType<ProblemDetails>(409)]
    public async Task<ActionResult<MilestoneDto>> Update(int milestoneId, UpdateMilestoneCommand body, CancellationToken ct)
        => Ok(await milestones.UpdateAsync(milestoneId, body, ct));

    [HttpPost("api/milestones/{milestoneId:int}/complete")]
    [Authorize(Policy = Policies.ManageProjects)]
    [ProducesResponseType<MilestoneDto>(200)]
    [ProducesResponseType<ProblemDetails>(403)]
    [ProducesResponseType<ProblemDetails>(404)]
    [ProducesResponseType<ProblemDetails>(409)]
    public async Task<ActionResult<MilestoneDto>> Complete(int milestoneId, CompleteMilestoneCommand body, CancellationToken ct)
        => Ok(await milestones.CompleteAsync(milestoneId, body, ct));
}
