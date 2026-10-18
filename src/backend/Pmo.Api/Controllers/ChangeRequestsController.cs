using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pmo.Api.Authorization;
using Pmo.Application.Common;
using Pmo.Application.Features.ChangeRequests;
using Pmo.Application.Features.ChangeRequests.Commands;
using Pmo.Application.Features.ChangeRequests.Dtos;
using Pmo.Application.Features.ChangeRequests.Queries;

namespace Pmo.Api.Controllers;

/// <summary>Epic E. Raising, editing, submitting and withdrawing are open to project members; deciding is the approvals route below.</summary>
[ApiController]
public class ChangeRequestsController(ChangeRequestService service) : ControllerBase
{
    [HttpGet("api/projects/{projectId:int}/change-requests")]
    [ProducesResponseType<PagedResult<ChangeRequestDto>>(200)]
    [ProducesResponseType<ProblemDetails>(404)]
    public async Task<ActionResult<PagedResult<ChangeRequestDto>>> List(int projectId, [FromQuery] ChangeRequestSearchQuery query, CancellationToken ct)
        => Ok(await service.ListForProjectAsync(projectId, query, ct));

    [HttpPost("api/projects/{projectId:int}/change-requests")]
    [ProducesResponseType<ChangeRequestDto>(201)]
    [ProducesResponseType<ProblemDetails>(400)]
    [ProducesResponseType<ProblemDetails>(404)]
    public async Task<ActionResult<ChangeRequestDto>> Create(int projectId, CreateChangeRequestCommand body, CancellationToken ct)
    {
        var created = await service.CreateAsync(projectId, body, ct);
        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    [HttpGet("api/change-requests/{id:int}")]
    [ProducesResponseType<ChangeRequestDto>(200)]
    [ProducesResponseType<ProblemDetails>(404)]
    public async Task<ActionResult<ChangeRequestDto>> Get(int id, CancellationToken ct) => Ok(await service.GetAsync(id, ct));

    /// <summary>Draft or returned requests only, by the requester only.</summary>
    [HttpPut("api/change-requests/{id:int}")]
    [ProducesResponseType<ChangeRequestDto>(200)]
    [ProducesResponseType<ProblemDetails>(400)]
    [ProducesResponseType<ProblemDetails>(403)]
    [ProducesResponseType<ProblemDetails>(404)]
    [ProducesResponseType<ProblemDetails>(409)]
    public async Task<ActionResult<ChangeRequestDto>> Update(int id, UpdateChangeRequestCommand body, CancellationToken ct)
        => Ok(await service.UpdateAsync(id, body, ct));

    [HttpPost("api/change-requests/{id:int}/submit")]
    [ProducesResponseType<ChangeRequestDto>(200)]
    [ProducesResponseType<ProblemDetails>(403)]
    [ProducesResponseType<ProblemDetails>(404)]
    [ProducesResponseType<ProblemDetails>(409)]
    public async Task<ActionResult<ChangeRequestDto>> Submit(int id, SubmitChangeRequestCommand body, CancellationToken ct)
        => Ok(await service.SubmitAsync(id, body, ct));

    [HttpPost("api/change-requests/{id:int}/withdraw")]
    [ProducesResponseType<ChangeRequestDto>(200)]
    [ProducesResponseType<ProblemDetails>(403)]
    [ProducesResponseType<ProblemDetails>(404)]
    [ProducesResponseType<ProblemDetails>(409)]
    public async Task<ActionResult<ChangeRequestDto>> Withdraw(int id, WithdrawChangeRequestCommand body, CancellationToken ct)
        => Ok(await service.WithdrawAsync(id, body, ct));

    [HttpGet("api/change-requests/{id:int}/history")]
    [ProducesResponseType<List<ChangeRequestHistoryDto>>(200)]
    [ProducesResponseType<ProblemDetails>(404)]
    public async Task<ActionResult<List<ChangeRequestHistoryDto>>> History(int id, CancellationToken ct) => Ok(await service.HistoryAsync(id, ct));

    /// <summary>E2. Submitted requests of projects the caller manages, excluding their own.</summary>
    [HttpGet("api/approvals/change-requests")]
    [Authorize(Policy = Policies.CanDecide)]
    [ProducesResponseType<PagedResult<ChangeRequestDto>>(200)]
    [ProducesResponseType<ProblemDetails>(403)]
    public async Task<ActionResult<PagedResult<ChangeRequestDto>>> ReviewQueue([FromQuery] ChangeRequestSearchQuery query, CancellationToken ct)
        => Ok(await service.ReviewQueueAsync(query, ct));

    /// <summary>E3. Contributor => 403 (policy). Requester => 403 self_approval_forbidden. Stale version or not submitted => 409.</summary>
    [HttpPost("api/approvals/change-requests/{id:int}/decision")]
    [Authorize(Policy = Policies.CanDecide)]
    [ProducesResponseType<ChangeRequestDto>(200)]
    [ProducesResponseType<ProblemDetails>(400)]
    [ProducesResponseType<ProblemDetails>(403)]
    [ProducesResponseType<ProblemDetails>(404)]
    [ProducesResponseType<ProblemDetails>(409)]
    public async Task<ActionResult<ChangeRequestDto>> Decide(int id, DecideChangeRequestCommand body, CancellationToken ct)
        => Ok(await service.DecideAsync(id, body, ct));
}
