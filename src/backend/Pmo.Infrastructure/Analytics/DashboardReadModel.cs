using Dapper;
using Npgsql;
using Pmo.Application.Common;
using Pmo.Application.Features.Dashboard;
using Pmo.Application.Features.Finance.Dtos;

namespace Pmo.Infrastructure.Analytics;

/// <summary>
/// Runs the dashboard SQL files in Analytics/Dashboard. Each query is "WITH fp AS (shared filter) ..." plus its own body, so the
/// filter is written once. Everything the user can influence is a parameter; the only text spliced into SQL are our own files.
/// </summary>
public sealed class DashboardReadModel(NpgsqlDataSource dataSource) : IDashboardReadModel
{
    private static readonly string FilteredProjects = SqlResources.Read("filtered_projects.sql");
    private static readonly string PerProjectFinance = SqlResources.Read("per_project_finance.sql");

    /// <summary>The full statement for one query file. /*FINANCE*/ marks where the per-project finance CTEs are included.</summary>
    private static string Build(string fileName) =>
        "WITH fp AS (\n" + FilteredProjects + "\n)\n" + SqlResources.Read(fileName).Replace("/*FINANCE*/", PerProjectFinance);

    private static readonly string KpiSql = Build("kpis.sql");
    private static readonly string ByProjectSql = Build("finance_by_project.sql");
    private static readonly string MonthlySql = Build("finance_monthly.sql");
    private static readonly string ByCategorySql = Build("finance_by_category.sql");
    private static readonly string OverdueSql = Build("overdue_by_owner.sql");
    private static readonly string MatrixSql = Build("risk_matrix.sql");
    private static readonly string CostsSql = Build("drilldown_costs.sql");
    private static readonly string TasksSql = Build("drilldown_tasks.sql");
    private static readonly string RisksSql = Build("drilldown_risks.sql");

    public async Task<DashboardKpisDto> KpisAsync(DashboardFilter f, int[] scopeIds, DateOnly asOf, CancellationToken ct)
    {
        var k = await QuerySingleAsync<KpiRow>(KpiSql, Params(f, scopeIds, asOf), ct);
        return new DashboardKpisDto(k.TotalProjects, k.ActiveProjects, k.PortfolioProgressPct, k.OverdueTasks, k.MilestonesDueSoon, k.OpenHighRisks,
            k.BlockedTasks, k.PendingChangeRequests, k.CurrentBudget, k.ActualSpend, k.RemainingBudget, k.Utilization, k.ForecastToComplete, k.Eac,
            k.ProjectedVariance, k.ProjectedOverrunProjects, k.ProjectedOverrunAmount, asOf);
    }

    public async Task<List<ProjectFinanceBarDto>> FinanceByProjectAsync(DashboardFilter f, int[] scopeIds, DateOnly asOf, CancellationToken ct) =>
        (await QueryAsync<ByProjectRow>(ByProjectSql, Params(f, scopeIds, asOf), ct))
        .Select(r => new ProjectFinanceBarDto(r.ProjectId, r.Code, r.Name, r.CurrentBudget, r.ActualSpend, r.ForecastToComplete, r.Eac)).ToList();

    public async Task<List<MonthlyPointDto>> FinanceMonthlyAsync(DashboardFilter f, int[] scopeIds, DateOnly asOf, CancellationToken ct) =>
        (await QueryAsync<MonthlyRow>(MonthlySql, Params(f, scopeIds, asOf), ct)).Select(r => new MonthlyPointDto(r.Month, r.ActualSpend, r.Forecast)).ToList();

    public async Task<List<CategorySpendDto>> FinanceByCategoryAsync(DashboardFilter f, int[] scopeIds, DateOnly asOf, CancellationToken ct) =>
        (await QueryAsync<CategoryRow>(ByCategorySql, Params(f, scopeIds, asOf), ct))
        .Select(r => new CategorySpendDto(r.CategoryId, r.Code, r.Name, r.ActualSpend, r.ForecastToComplete)).ToList();

    public async Task<List<OverdueByOwnerDto>> OverdueByOwnerAsync(DashboardFilter f, int[] scopeIds, DateOnly asOf, CancellationToken ct) =>
        (await QueryAsync<OverdueRow>(OverdueSql, Params(f, scopeIds, asOf), ct)).Select(r => new OverdueByOwnerDto(r.UserId, r.OwnerName, r.OverdueTasks)).ToList();

    public async Task<List<RiskMatrixCellDto>> RiskMatrixAsync(DashboardFilter f, int[] scopeIds, DateOnly asOf, CancellationToken ct) =>
        (await QueryAsync<MatrixRow>(MatrixSql, Params(f, scopeIds, asOf), ct)).Select(r => new RiskMatrixCellDto(r.Probability, r.Impact, r.OpenCount)).ToList();

    public async Task<DrilldownCostsResult> CostsDrilldownAsync(DashboardFilter f, int[] scopeIds, DateOnly asOf, CostDrilldownOptions o, CancellationToken ct)
    {
        var parameters = Params(f, scopeIds, asOf);
        parameters.Add("page_size", o.PageSize);
        parameters.Add("offset", (o.Page - 1) * o.PageSize);
        var rows = await QueryAsync<CostRow>(CostsSql, parameters, ct);
        var items = rows.Select(r => new DrilldownCostDto(r.Id, r.ProjectCode, r.CategoryName, r.TxnDate, r.Amount, r.Description, r.VendorName, r.ReferenceNo)).ToList();
        return new DrilldownCostsResult(items, o.Page, o.PageSize, rows.FirstOrDefault()?.TotalCount ?? 0, rows.FirstOrDefault()?.TotalAmount ?? 0m);
    }

    public async Task<PagedResult<DrilldownTaskDto>> TasksDrilldownAsync(DashboardFilter f, int[] scopeIds, DateOnly asOf, TaskDrilldownOptions o, CancellationToken ct)
    {
        var parameters = Params(f, scopeIds, asOf);
        parameters.Add("overdue", o.Overdue);
        parameters.Add("assignee_id", o.AssigneeId);
        parameters.Add("unassigned", o.Unassigned);
        parameters.Add("page_size", o.PageSize);
        parameters.Add("offset", (o.Page - 1) * o.PageSize);
        var rows = await QueryAsync<TaskRow>(TasksSql, parameters, ct);
        var items = rows.Select(r => new DrilldownTaskDto(r.Id, r.ProjectCode, r.Title, r.AssigneeName, r.Status, r.Priority, r.DueDate, r.DaysOverdue, r.ProgressPct)).ToList();
        return new PagedResult<DrilldownTaskDto>(items, o.Page, o.PageSize, rows.FirstOrDefault()?.TotalCount ?? 0);
    }

    public async Task<PagedResult<DrilldownRiskDto>> RisksDrilldownAsync(DashboardFilter f, int[] scopeIds, DateOnly asOf, RiskDrilldownOptions o, CancellationToken ct)
    {
        var parameters = Params(f, scopeIds, asOf);
        parameters.Add("probability", o.Probability);
        parameters.Add("impact", o.Impact);
        parameters.Add("high_exposure", o.HighExposure);
        parameters.Add("page_size", o.PageSize);
        parameters.Add("offset", (o.Page - 1) * o.PageSize);
        var rows = await QueryAsync<RiskRow>(RisksSql, parameters, ct);
        var items = rows.Select(r => new DrilldownRiskDto(r.Id, r.ProjectCode, r.Type, r.Title, r.OwnerName, r.Probability, r.Impact, r.Score, r.Status, r.DueDate)).ToList();
        return new PagedResult<DrilldownRiskDto>(items, o.Page, o.PageSize, rows.FirstOrDefault()?.TotalCount ?? 0);
    }

    // ---- plumbing ----

    private static DynamicParameters Params(DashboardFilter f, int[] scopeIds, DateOnly asOf)
    {
        var p = new DynamicParameters();
        p.Add("as_of", asOf);
        p.Add("scope_ids", scopeIds);
        p.Add("project_id", f.ProjectId);
        p.Add("department_id", f.DepartmentId);
        p.Add("manager_id", f.ManagerId);
        p.Add("status", f.Status);
        p.Add("from", f.From);
        p.Add("to", f.To);
        p.Add("category_id", f.CategoryId);
        return p;
    }

    private async Task<List<T>> QueryAsync<T>(string sql, DynamicParameters parameters, CancellationToken ct)
    {
        await using var connection = await dataSource.OpenConnectionAsync(ct);
        return (await connection.QueryAsync<T>(new CommandDefinition(sql, parameters, cancellationToken: ct))).AsList();
    }

    private async Task<T> QuerySingleAsync<T>(string sql, DynamicParameters parameters, CancellationToken ct) =>
        (await QueryAsync<T>(sql, parameters, ct)).Single();

    // Row shapes: property names match the snake_case columns (Dapper ignores underscores).
    private class KpiRow
    {
        public int TotalProjects { get; set; }
        public int ActiveProjects { get; set; }
        public decimal? PortfolioProgressPct { get; set; }
        public int OverdueTasks { get; set; }
        public int MilestonesDueSoon { get; set; }
        public int OpenHighRisks { get; set; }
        public int BlockedTasks { get; set; }
        public int PendingChangeRequests { get; set; }
        public decimal CurrentBudget { get; set; }
        public decimal ActualSpend { get; set; }
        public decimal RemainingBudget { get; set; }
        public decimal? Utilization { get; set; }
        public decimal ForecastToComplete { get; set; }
        public decimal Eac { get; set; }
        public decimal ProjectedVariance { get; set; }
        public int ProjectedOverrunProjects { get; set; }
        public decimal ProjectedOverrunAmount { get; set; }
    }

    private class ByProjectRow { public int ProjectId { get; set; } public string Code { get; set; } = ""; public string Name { get; set; } = ""; public decimal CurrentBudget { get; set; } public decimal ActualSpend { get; set; } public decimal ForecastToComplete { get; set; } public decimal Eac { get; set; } }
    private class MonthlyRow { public DateOnly Month { get; set; } public decimal ActualSpend { get; set; } public decimal Forecast { get; set; } }
    private class CategoryRow { public int CategoryId { get; set; } public string Code { get; set; } = ""; public string Name { get; set; } = ""; public decimal ActualSpend { get; set; } public decimal ForecastToComplete { get; set; } }
    private class OverdueRow { public int? UserId { get; set; } public string OwnerName { get; set; } = ""; public int OverdueTasks { get; set; } }
    private class MatrixRow { public int Probability { get; set; } public int Impact { get; set; } public int OpenCount { get; set; } }

    private class CostRow
    {
        public long Id { get; set; }
        public string ProjectCode { get; set; } = "";
        public string CategoryName { get; set; } = "";
        public DateOnly TxnDate { get; set; }
        public decimal Amount { get; set; }
        public string Description { get; set; } = "";
        public string? VendorName { get; set; }
        public string? ReferenceNo { get; set; }
        public int TotalCount { get; set; }
        public decimal TotalAmount { get; set; }
    }

    private class TaskRow
    {
        public int Id { get; set; }
        public string ProjectCode { get; set; } = "";
        public string Title { get; set; } = "";
        public string AssigneeName { get; set; } = "";
        public string Status { get; set; } = "";
        public string Priority { get; set; } = "";
        public DateOnly DueDate { get; set; }
        public int DaysOverdue { get; set; }
        public int ProgressPct { get; set; }
        public int TotalCount { get; set; }
    }

    private class RiskRow
    {
        public int Id { get; set; }
        public string ProjectCode { get; set; } = "";
        public string Type { get; set; } = "";
        public string Title { get; set; } = "";
        public string OwnerName { get; set; } = "";
        public int Probability { get; set; }
        public int Impact { get; set; }
        public int Score { get; set; }
        public string Status { get; set; } = "";
        public DateOnly? DueDate { get; set; }
        public int TotalCount { get; set; }
    }
}
