using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pmo.Api.Authorization;
using Pmo.Application.Features.Projects;
using Pmo.Application.Features.Projects.Commands;
using Pmo.Application.Features.Projects.Dtos;

namespace Pmo.Api.Controllers;

[ApiController]
[Route("api/projects/{projectId:int}/members")]
public class ProjectMembersController(ProjectMemberService members) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<List<MemberDto>>(200)]
    [ProducesResponseType<ProblemDetails>(404)]
    public async Task<ActionResult<List<MemberDto>>> List(int projectId, CancellationToken ct)
        => Ok(await members.ListAsync(projectId, ct));

    [HttpPost]
    [Authorize(Policy = Policies.ManageProjects)]
    [ProducesResponseType<MemberDto>(201)]
    [ProducesResponseType<ProblemDetails>(400)]
    [ProducesResponseType<ProblemDetails>(403)]
    [ProducesResponseType<ProblemDetails>(404)]
    [ProducesResponseType<ProblemDetails>(409)]
    public async Task<ActionResult<MemberDto>> Add(int projectId, AddMemberCommand body, CancellationToken ct)
    {
        var member = await members.AddAsync(projectId, body, ct);
        return Created($"/api/projects/{projectId}/members", member);
    }

    [HttpDelete("{userId:int}")]
    [Authorize(Policy = Policies.ManageProjects)]
    [ProducesResponseType(204)]
    [ProducesResponseType<ProblemDetails>(403)]
    [ProducesResponseType<ProblemDetails>(404)]
    [ProducesResponseType<ProblemDetails>(409)]
    public async Task<IActionResult> Remove(int projectId, int userId, CancellationToken ct)
    {
        await members.RemoveAsync(projectId, userId, ct);
        return NoContent();
    }
}
