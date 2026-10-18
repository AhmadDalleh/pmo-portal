using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pmo.Api.Authorization;
using Pmo.Application.Features.Snapshots;
using Pmo.Application.Features.Snapshots.Dtos;

namespace Pmo.Api.Controllers;

[ApiController]
public class SnapshotsController(SnapshotService snapshots) : ControllerBase
{
    [HttpGet("api/projects/{projectId:int}/progress-history")]
    [ProducesResponseType<List<ProgressSnapshotDto>>(200)]
    [ProducesResponseType<ProblemDetails>(404)]
    public async Task<ActionResult<List<ProgressSnapshotDto>>> History(int projectId, CancellationToken ct)
        => Ok(await snapshots.HistoryAsync(projectId, ct));

    /// <summary>C7. Administrator only. Safe to repeat: the same date is updated, not duplicated.</summary>
    [HttpPost("api/admin/snapshots/run")]
    [Authorize(Policy = Policies.AdminOnly)]
    [ProducesResponseType<SnapshotRunResult>(200)]
    [ProducesResponseType<ProblemDetails>(403)]
    public async Task<ActionResult<SnapshotRunResult>> Run([FromQuery] DateOnly? date, CancellationToken ct)
        => Ok(await snapshots.RunAsync(date, ct));
}
