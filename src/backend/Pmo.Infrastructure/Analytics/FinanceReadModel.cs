using Dapper;
using Npgsql;
using Pmo.Application.Features.Finance;
using Pmo.Application.Features.Finance.Dtos;

namespace Pmo.Infrastructure.Analytics;

/// <summary>
/// Spend and forecast per category and per month. Actuals and forecasts are aggregated in separate CTEs and joined
/// afterwards, so neither can multiply the other (database-design.md section 7).
/// </summary>
public sealed class FinanceReadModel(NpgsqlDataSource dataSource) : IFinanceReadModel
{
    private const string ByCategorySql = """
        WITH actual AS (
            SELECT category_id, SUM(amount) AS actual_spend
            FROM cost_transactions
            WHERE project_id = @project_id AND status = 'posted' AND txn_date <= @as_of::date
            GROUP BY category_id
        ),
        ftc AS (
            SELECT category_id, SUM(amount) AS forecast_to_complete
            FROM financial_forecasts
            WHERE project_id = @project_id AND period_month > date_trunc('month', @as_of::date)::date
            GROUP BY category_id
        )
        SELECT c.id AS category_id, c.code, c.name,
               COALESCE(a.actual_spend, 0) AS actual_spend, COALESCE(f.forecast_to_complete, 0) AS forecast_to_complete
        FROM cost_categories c
        LEFT JOIN actual a ON a.category_id = c.id
        LEFT JOIN ftc f ON f.category_id = c.id
        WHERE a.category_id IS NOT NULL OR f.category_id IS NOT NULL
        ORDER BY c.name
        """;

    private const string MonthlySql = """
        WITH actual AS (
            SELECT date_trunc('month', txn_date)::date AS month, SUM(amount) AS actual_spend
            FROM cost_transactions
            WHERE project_id = @project_id AND status = 'posted' AND txn_date <= @as_of::date
              AND (@category_id::int IS NULL OR category_id = @category_id::int)
            GROUP BY 1
        ),
        forecast AS (
            SELECT period_month AS month, SUM(amount) AS forecast
            FROM financial_forecasts
            WHERE project_id = @project_id AND period_month > date_trunc('month', @as_of::date)::date
              AND (@category_id::int IS NULL OR category_id = @category_id::int)
            GROUP BY 1
        )
        SELECT COALESCE(a.month, f.month) AS month, COALESCE(a.actual_spend, 0) AS actual_spend, COALESCE(f.forecast, 0) AS forecast
        FROM actual a
        FULL OUTER JOIN forecast f ON f.month = a.month
        ORDER BY 1
        """;

    public async Task<List<CategorySpendDto>> ByCategoryAsync(int projectId, DateOnly asOf, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync<CategorySpendRow>(
            new CommandDefinition(ByCategorySql, new { project_id = projectId, as_of = asOf }, cancellationToken: cancellationToken));
        return rows.Select(r => new CategorySpendDto(r.CategoryId, r.Code, r.Name, r.ActualSpend, r.ForecastToComplete)).ToList();
    }

    public async Task<List<MonthlyPointDto>> MonthlyAsync(int projectId, DateOnly asOf, int? categoryId, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync<MonthlyRow>(new CommandDefinition(MonthlySql,
            new { project_id = projectId, as_of = asOf, category_id = categoryId }, cancellationToken: cancellationToken));
        return rows.Select(r => new MonthlyPointDto(r.Month, r.ActualSpend, r.Forecast)).ToList();
    }

    private class CategorySpendRow
    {
        public int CategoryId { get; set; }
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public decimal ActualSpend { get; set; }
        public decimal ForecastToComplete { get; set; }
    }

    private class MonthlyRow
    {
        public DateOnly Month { get; set; }
        public decimal ActualSpend { get; set; }
        public decimal Forecast { get; set; }
    }
}
