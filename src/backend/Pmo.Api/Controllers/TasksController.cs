using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pmo.Api.Authorization;
using Pmo.Application.Common;
using Pmo.Application.Features.Tasks;
using Pmo.Application.Features.Tasks.Commands;
using Pmo.Application.Features.Tasks.Dtos;
using Pmo.Application.Features.Tasks.Queries;

namespace Pmo.Api.Controllers;

[ApiController]
public class TasksController(TaskService tasks) : ControllerBase
{
    /// <summary>C1. Tasks of one project with filters, sort and paging.</summary>
    [HttpGet("api/projects/{projectId:int}/tasks")]
    [ProducesResponseType<PagedResult<TaskDto>>(200)]
    [ProducesResponseType<ProblemDetails>(400)]
    [ProducesResponseType<ProblemDetails>(404)]
    public async Task<ActionResult<PagedResult<TaskDto>>> List(
        int projectId, [FromQuery] TaskSearchQuery query, [FromQuery] DateOnly? asOf, CancellationToken ct)
        => Ok(await tasks.ListAsync(projectId, query, asOf, ct));

    /// <summary>C3. The caller's own open tasks across projects.</summary>
    [HttpGet("api/me/tasks")]
    [ProducesResponseType<PagedResult<TaskDto>>(200)]
    public async Task<ActionResult<PagedResult<TaskDto>>> Mine([FromQuery] MyTasksQuery query, [FromQuery] DateOnly? asOf, CancellationToken ct)
        => Ok(await tasks.MyTasksAsync(query, asOf, ct));

    [HttpGet("api/tasks/{taskId:int}")]
    [ProducesResponseType<TaskDto>(200)]
    [ProducesResponseType<ProblemDetails>(404)]
    public async Task<ActionResult<TaskDto>> Get(int taskId, [FromQuery] DateOnly? asOf, CancellationToken ct)
        => Ok(await tasks.GetAsync(taskId, asOf, ct));

    [HttpPost("api/projects/{projectId:int}/tasks")]
    [Authorize(Policy = Policies.ManageProjects)]
    [ProducesResponseType<TaskDto>(201)]
    [ProducesResponseType<ProblemDetails>(400)]
    [ProducesResponseType<ProblemDetails>(403)]
    [ProducesResponseType<ProblemDetails>(404)]
    public async Task<ActionResult<TaskDto>> Create(int projectId, CreateTaskCommand body, CancellationToken ct)
    {
        var created = await tasks.CreateAsync(projectId, body, ct);
        return CreatedAtAction(nameof(Get), new { taskId = created.Id }, created);
    }

    [HttpPut("api/tasks/{taskId:int}")]
    [Authorize(Policy = Policies.ManageProjects)]
    [ProducesResponseType<TaskDto>(200)]
    [ProducesResponseType<ProblemDetails>(400)]
    [ProducesResponseType<ProblemDetails>(403)]
    [ProducesResponseType<ProblemDetails>(404)]
    [ProducesResponseType<ProblemDetails>(409)]
    public async Task<ActionResult<TaskDto>> Update(int taskId, UpdateTaskCommand body, CancellationToken ct)
        => Ok(await tasks.UpdateAsync(taskId, body, ct));

    /// <summary>C2. Any member may call it; the service allows only managers and the task assignee.</summary>
    [HttpPatch("api/tasks/{taskId:int}/progress")]
    [ProducesResponseType<TaskDto>(200)]
    [ProducesResponseType<ProblemDetails>(400)]
    [ProducesResponseType<ProblemDetails>(403)]
    [ProducesResponseType<ProblemDetails>(404)]
    [ProducesResponseType<ProblemDetails>(409)]
    public async Task<ActionResult<TaskDto>> UpdateProgress(int taskId, UpdateTaskProgressCommand body, CancellationToken ct)
        => Ok(await tasks.UpdateProgressAsync(taskId, body, ct));
}
