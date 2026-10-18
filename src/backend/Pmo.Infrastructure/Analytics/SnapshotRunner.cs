using Dapper;
using Npgsql;
using Pmo.Application.Features.Snapshots;

namespace Pmo.Infrastructure.Analytics;

/// <summary>
/// One upsert statement. KPI definitions are those of database-design.md section 6: weighted progress of non-cancelled tasks,
/// posted spend up to the snapshot date. Completed and cancelled projects are no longer snapshotted.
/// </summary>
public sealed class SnapshotRunner(NpgsqlDataSource dataSource) : ISnapshotRunner
{
    private const string Sql = """
        INSERT INTO progress_snapshots (project_id, snapshot_date, progress_pct, tasks_total, tasks_done, actual_spend)
        SELECT p.id, @snapshot_date::date,
               ROUND(COALESCE(pr.progress_pct, 0), 2), COALESCE(tc.tasks_total, 0), COALESCE(tc.tasks_done, 0), COALESCE(a.actual_spend, 0)
        FROM projects p
        LEFT JOIN (SELECT project_id, SUM(weight * progress_pct)::numeric / NULLIF(SUM(weight), 0) AS progress_pct
                   FROM tasks WHERE status <> 'cancelled' GROUP BY project_id) pr ON pr.project_id = p.id
        LEFT JOIN (SELECT project_id, COUNT(*)::int AS tasks_total, (COUNT(*) FILTER (WHERE status = 'done'))::int AS tasks_done
                   FROM tasks WHERE status <> 'cancelled' GROUP BY project_id) tc ON tc.project_id = p.id
        LEFT JOIN (SELECT project_id, SUM(amount) AS actual_spend
                   FROM cost_transactions WHERE status = 'posted' AND txn_date <= @snapshot_date::date GROUP BY project_id) a ON a.project_id = p.id
        WHERE p.status IN ('proposed', 'active', 'on_hold')
        ON CONFLICT (project_id, snapshot_date) DO UPDATE
           SET progress_pct = EXCLUDED.progress_pct, tasks_total = EXCLUDED.tasks_total,
               tasks_done = EXCLUDED.tasks_done, actual_spend = EXCLUDED.actual_spend
        """;

    public async Task<int> RunAsync(DateOnly date, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        return await connection.ExecuteAsync(new CommandDefinition(Sql, new { snapshot_date = date }, cancellationToken: cancellationToken));
    }
}
