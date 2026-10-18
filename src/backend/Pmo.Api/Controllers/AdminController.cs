using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pmo.Api.Authorization;
using Pmo.Application.Common;
using Pmo.Application.Common.Abstractions;
using Pmo.Application.Features.Admin;

namespace Pmo.Api.Controllers;

/// <summary>Administrator-only screens: users and roles (A4), audit log (A5), automation monitor (H3).</summary>
[ApiController]
[Authorize(Policy = Policies.AdminOnly)]
[Route("api/admin")]
public class AdminController(IUserAdmin userAdmin, AdminService admin) : ControllerBase
{
    [HttpGet("users")]
    [ProducesResponseType<List<UserSummary>>(200)]
    [ProducesResponseType<ProblemDetails>(403)]
    public async Task<ActionResult<List<UserSummary>>> Users(CancellationToken ct) => Ok(await userAdmin.ListAsync(ct));

    [HttpPut("users/{id:int}/role")]
    [ProducesResponseType<UserSummary>(200)]
    [ProducesResponseType<ProblemDetails>(400)]
    [ProducesResponseType<ProblemDetails>(404)]
    [ProducesResponseType<ProblemDetails>(409)]
    public async Task<ActionResult<UserSummary>> ChangeRole(int id, ChangeRoleCommand body, CancellationToken ct)
        => Ok(await userAdmin.ChangeRoleAsync(id, body.Role, ct));

    [HttpPut("users/{id:int}/status")]
    [ProducesResponseType<UserSummary>(200)]
    [ProducesResponseType<ProblemDetails>(404)]
    [ProducesResponseType<ProblemDetails>(409)]
    public async Task<ActionResult<UserSummary>> SetStatus(int id, SetUserStatusCommand body, CancellationToken ct)
        => Ok(await userAdmin.SetActiveAsync(id, body.IsActive, ct));

    [HttpGet("audit-events")]
    [ProducesResponseType<PagedResult<AuditEventDto>>(200)]
    public async Task<ActionResult<PagedResult<AuditEventDto>>> Audit([FromQuery] AuditSearchQuery query, CancellationToken ct)
        => Ok(await admin.AuditAsync(query, ct));

    [HttpGet("automation/runs")]
    [ProducesResponseType<PagedResult<AutomationRunDto>>(200)]
    public async Task<ActionResult<PagedResult<AutomationRunDto>>> Runs([FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
        => Ok(await admin.RunsAsync(page, pageSize, ct));

    [HttpGet("automation/outbox")]
    [ProducesResponseType<PagedResult<OutboxMessageDto>>(200)]
    [ProducesResponseType<ProblemDetails>(400)]
    public async Task<ActionResult<PagedResult<OutboxMessageDto>>> Outbox(
        [FromQuery] string? status, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
        => Ok(await admin.OutboxAsync(status, page, pageSize, ct));

    [HttpPost("automation/outbox/{id:long}/retry")]
    [ProducesResponseType<OutboxMessageDto>(200)]
    [ProducesResponseType<ProblemDetails>(404)]
    [ProducesResponseType<ProblemDetails>(409)]
    public async Task<ActionResult<OutboxMessageDto>> Retry(long id, CancellationToken ct) => Ok(await admin.RetryAsync(id, ct));
}
