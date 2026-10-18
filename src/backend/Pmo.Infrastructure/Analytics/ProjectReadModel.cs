using Dapper;
using Npgsql;
using Pmo.Application.Features.Projects;
using Pmo.Application.Features.Projects.Dtos;
using Pmo.Application.Features.Projects.Queries;

namespace Pmo.Infrastructure.Analytics;

/// <summary>Runs ProjectPortfolio.sql (database-design.md section 6). All values are parameters; the ORDER BY comes from a fixed map.</summary>
public sealed class ProjectReadModel(NpgsqlDataSource dataSource) : IProjectReadModel
{
    // Sort key from the API -> column of the "kpi" CTE. User text never reaches the SQL, only these constants.
    private static readonly Dictionary<string, string> SortColumns = new()
    {
        ["code"] = "code", ["name"] = "name", ["department"] = "department_name", ["owner"] = "owner_name",
        ["status"] = "status", ["start"] = "start_date", ["end"] = "planned_end_date",
        ["progress"] = "progress_pct", ["utilization"] = "utilization"
    };

    private static readonly string PortfolioSql = SqlResources.Read("ProjectPortfolio.sql");

    public async Task<List<ProjectKpiRow>> SearchAsync(ProjectSearchQuery query, int[] scopeIds, DateOnly asOf, CancellationToken cancellationToken)
    {
        var direction = query.Dir == "desc" ? "DESC" : "ASC";
        var orderBy = $"ORDER BY {SortColumns[query.Sort]} {direction} NULLS LAST, id";
        var sql = PortfolioSql.Replace("/*ORDER_BY*/", orderBy);

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync<ProjectKpiRow>(new CommandDefinition(sql, new
        {
            as_of = asOf, scope_ids = scopeIds, search = query.Search, department_id = query.DepartmentId,
            owner_id = query.OwnerId, status = query.Status, from = query.From, to = query.To,
            page_size = query.PageSize, offset = (query.Page - 1) * query.PageSize
        }, cancellationToken: cancellationToken));
        return rows.AsList();
    }

    public async Task<ProjectKpiRow?> GetKpisAsync(int projectId, DateOnly asOf, CancellationToken cancellationToken)
    {
        var sql = PortfolioSql.Replace("/*ORDER_BY*/", "ORDER BY id");
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync<ProjectKpiRow>(new CommandDefinition(sql, new
        {
            as_of = asOf, scope_ids = new[] { projectId }, search = (string?)null, department_id = (int?)null,
            owner_id = (int?)null, status = (string?)null, from = (DateOnly?)null, to = (DateOnly?)null,
            page_size = 1, offset = 0
        }, cancellationToken: cancellationToken));
        return rows.FirstOrDefault();
    }

    public async Task<NextMilestoneDto?> NextMilestoneAsync(int projectId, DateOnly asOf, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT id, name, due_date AS DueDate, (due_date < @as_of) AS IsOverdue
            FROM milestones
            WHERE project_id = @project_id AND status = 'pending'
            ORDER BY due_date, id
            LIMIT 1
            """;
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        return await connection.QueryFirstOrDefaultAsync<NextMilestoneDto>(
            new CommandDefinition(sql, new { project_id = projectId, as_of = asOf }, cancellationToken: cancellationToken));
    }
}
