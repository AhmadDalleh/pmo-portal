using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Pmo.Application.Common;
using Pmo.Application.Common.Abstractions;
using Pmo.Application.Features.Snapshots.Dtos;
using Pmo.Domain.Governance;

namespace Pmo.Application.Features.Snapshots;

/// <summary>Story C7: progress history per project, and the Administrator's "run today's snapshot" action.</summary>
public sealed class SnapshotService(
    IPmoDbContext db, ICurrentUser user, IProjectScope scope, ISnapshotRunner runner, IOptions<ReportingOptions> reporting,
    TimeProvider clock, ICorrelationIdAccessor correlation)
{
    public async Task<List<ProgressSnapshotDto>> HistoryAsync(int projectId, CancellationToken ct)
    {
        await scope.EnsureCanViewAsync(projectId, ct);
        return await db.ProgressSnapshots.AsNoTracking().Where(s => s.ProjectId == projectId).OrderBy(s => s.SnapshotDate)
            .Select(s => new ProgressSnapshotDto(s.SnapshotDate, s.ProgressPct, s.TasksTotal, s.TasksDone, s.ActualSpend)).ToListAsync(ct);
    }

    /// <summary>The caller must be an Administrator (policy on the route). The date defaults to the reporting date.</summary>
    public async Task<SnapshotRunResult> RunAsync(DateOnly? date, CancellationToken ct)
    {
        var day = reporting.Value.Resolve(date);
        var count = await runner.RunAsync(day, ct);

        db.AuditEvents.Add(AuditEvent.Create("snapshot.run", "ProgressSnapshot", day.ToString("yyyy-MM-dd"), user.Id, null,
            correlation.CorrelationId, clock.GetUtcNow(), new { projects = count }));
        await db.SaveChangesAsync(ct);
        return new SnapshotRunResult(day, count);
    }
}
