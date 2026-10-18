using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pmo.Api.Authorization;
using Pmo.Application.Common;
using Pmo.Application.Features.Projects;
using Pmo.Application.Features.Projects.Commands;
using Pmo.Application.Features.Projects.Dtos;
using Pmo.Application.Features.Projects.Queries;

namespace Pmo.Api.Controllers;

[ApiController]
[Route("api/projects")]
public class ProjectsController(ProjectService projects) : ControllerBase
{
    /// <summary>B3. Search, filter, sort and page the projects the caller may see.</summary>
    [HttpGet]
    [ProducesResponseType<PagedResult<ProjectListItemDto>>(200)]
    [ProducesResponseType<ProblemDetails>(400)]
    public async Task<ActionResult<PagedResult<ProjectListItemDto>>> List(
        [FromQuery] ProjectSearchQuery query, [FromQuery] DateOnly? asOf, CancellationToken ct)
        => Ok(await projects.ListAsync(query, asOf, ct));

    /// <summary>B4. 404 when the project does not exist or is outside the caller's scope.</summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType<ProjectDto>(200)]
    [ProducesResponseType<ProblemDetails>(404)]
    public async Task<ActionResult<ProjectDto>> Get(int id, CancellationToken ct) => Ok(await projects.GetAsync(id, ct));

    [HttpGet("{id:int}/summary")]
    [ProducesResponseType<ProjectSummaryDto>(200)]
    [ProducesResponseType<ProblemDetails>(404)]
    public async Task<ActionResult<ProjectSummaryDto>> Summary(int id, [FromQuery] DateOnly? asOf, CancellationToken ct)
        => Ok(await projects.GetSummaryAsync(id, asOf, ct));

    /// <summary>B1. Creates project, initial budget and owner membership in one transaction.</summary>
    [HttpPost]
    [Authorize(Policy = Policies.ManageProjects)]
    [ProducesResponseType<ProjectDto>(201)]
    [ProducesResponseType<ProblemDetails>(400)]
    [ProducesResponseType<ProblemDetails>(403)]
    [ProducesResponseType<ProblemDetails>(409)]
    public async Task<ActionResult<ProjectDto>> Create(CreateProjectCommand body, CancellationToken ct)
    {
        var created = await projects.CreateAsync(body, ct);
        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    /// <summary>B2. Send the project's current version; a stale version returns 409 concurrency_conflict.</summary>
    [HttpPut("{id:int}")]
    [Authorize(Policy = Policies.ManageProjects)]
    [ProducesResponseType<ProjectDto>(200)]
    [ProducesResponseType<ProblemDetails>(400)]
    [ProducesResponseType<ProblemDetails>(403)]
    [ProducesResponseType<ProblemDetails>(404)]
    [ProducesResponseType<ProblemDetails>(409)]
    public async Task<ActionResult<ProjectDto>> Update(int id, UpdateProjectCommand body, CancellationToken ct)
        => Ok(await projects.UpdateAsync(id, body, ct));
}
