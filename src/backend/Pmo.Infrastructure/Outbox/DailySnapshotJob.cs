using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Pmo.Application.Common;
using Pmo.Application.Features.Snapshots;
using Pmo.Domain.Automation;
using Pmo.Infrastructure.Persistence;

namespace Pmo.Infrastructure.Outbox;

/// <summary>
/// Story C7: once per day, snapshot every live project for the reporting date. Checks hourly whether today's run exists,
/// so a restart never snapshots twice (and the SQL is an upsert anyway). The run is logged in automation_runs.
/// </summary>
public sealed class DailySnapshotJob(IServiceScopeFactory scopes, IConfiguration configuration, ILogger<DailySnapshotJob> logger) : BackgroundService
{
    public const string JobName = "DailyProgressSnapshot";

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var minutes = Math.Max(1, configuration.GetValue("Automation:SnapshotCheckMinutes", 60));
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(minutes));
        do
        {
            try
            {
                using var scope = scopes.CreateScope();
                await RunIfDueAsync(scope.ServiceProvider, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Daily snapshot job failed; it will be retried at the next check");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private static async Task RunIfDueAsync(IServiceProvider services, CancellationToken ct)
    {
        var db = services.GetRequiredService<PmoDbContext>();
        var clock = services.GetRequiredService<TimeProvider>();
        var today = clock.GetUtcNow().UtcDateTime.Date;

        var alreadyRan = await db.AutomationRuns.AnyAsync(
            r => r.JobName == JobName && r.Status == RunStatus.Succeeded && r.StartedAt >= today, ct);
        if (alreadyRan)
        {
            return;
        }

        var started = clock.GetUtcNow();
        var date = services.GetRequiredService<IOptions<ReportingOptions>>().Value.DefaultAsOf;   // in production the default is today
        var count = await services.GetRequiredService<ISnapshotRunner>().RunAsync(date, ct);

        db.AutomationRuns.Add(new AutomationRun
        {
            JobName = JobName, StartedAt = started, FinishedAt = clock.GetUtcNow(), Status = RunStatus.Succeeded,
            MessagesProcessed = count, MessagesFailed = 0, CorrelationId = Guid.NewGuid().ToString("N")
        });
        await db.SaveChangesAsync(ct);
    }
}
