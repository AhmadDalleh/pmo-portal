using System.Net.Http.Json;
using Pmo.Application.Features.Dashboard;
using Pmo.IntegrationTests.Infrastructure;
using Shouldly;

namespace Pmo.IntegrationTests.Reporting;

/// <summary>
/// The reporting views (Power BI source) must give the same numbers as the dashboard API and as plain SQL over the base tables.
/// All comparisons run in the same database at the same moment, so other tests changing data cannot cause a false failure.
/// </summary>
[Collection(ApiCollection.Name)]
public class ReportingViewTests(PmoApiFactory factory)
{
    private async Task<DashboardKpisDto> KpisAsync()
    {
        var client = await factory.CreateClientForAsync(DemoUsers.Admin1);
        return (await client.GetFromJsonAsync<DashboardKpisDto>("/api/dashboard/kpis"))!;
    }

    [Fact]
    public async Task Views_GiveTheSameFinanceKpisAsTheDashboard()
    {
        var k = await KpisAsync();

        (await factory.ScalarAsync<decimal>("SELECT SUM(current_budget) FROM vw_project_financials")).ShouldBe(k.CurrentBudget);
        (await factory.ScalarAsync<decimal>("SELECT SUM(actual_spend) FROM vw_project_financials")).ShouldBe(k.ActualSpend);
        (await factory.ScalarAsync<decimal>("SELECT SUM(eac) FROM vw_project_financials")).ShouldBe(k.Eac);
        (await factory.ScalarAsync<decimal>("SELECT SUM(actual_spend) / SUM(current_budget) FROM vw_project_financials"))
            .ShouldBe(k.Utilization!.Value, 0.00001m);
        ((int)await factory.ScalarAsync<long>("SELECT count(*) FROM vw_project_financials WHERE is_projected_overrun"))
            .ShouldBe(k.ProjectedOverrunProjects);
    }

    [Fact]
    public async Task Views_GiveTheSameDeliveryAndRiskKpisAsTheDashboard()
    {
        var k = await KpisAsync();

        ((int)await factory.ScalarAsync<long>("SELECT count(*) FROM vw_task_facts WHERE is_overdue")).ShouldBe(k.OverdueTasks);
        ((int)await factory.ScalarAsync<long>("SELECT count(*) FROM vw_task_facts WHERE is_blocked")).ShouldBe(k.BlockedTasks);
        ((int)await factory.ScalarAsync<long>("SELECT count(*) FROM vw_risk_facts WHERE is_open_high_exposure")).ShouldBe(k.OpenHighRisks);
        ((int)await factory.ScalarAsync<long>("SELECT count(*) FROM vw_milestone_facts WHERE is_due_soon")).ShouldBe(k.MilestonesDueSoon);
        (await factory.ScalarAsync<decimal>("SELECT SUM(weighted_progress)::numeric / SUM(weight_for_progress) FROM vw_task_facts"))
            .ShouldBe(k.PortfolioProgressPct!.Value, 0.0001m);
    }

    [Fact]
    public async Task CostFacts_OnlyHoldPostedCostsUpToTheReportingDate_AndSumToTheProjectView()
    {
        var factsSum = await factory.ScalarAsync<decimal>("SELECT SUM(amount) FROM vw_cost_facts");
        var projectSum = await factory.ScalarAsync<decimal>("SELECT SUM(actual_spend) FROM vw_project_financials");
        var baseSum = await factory.ScalarAsync<decimal>(
            "SELECT SUM(amount) FROM cost_transactions WHERE status = 'posted' AND txn_date <= '2026-09-30'");

        factsSum.ShouldBe(baseSum);
        projectSum.ShouldBe(baseSum);   // aggregating in separate CTEs means no join multiplies the spend
        (await factory.ScalarAsync<long>("SELECT count(*) FROM vw_cost_facts WHERE txn_date > '2026-09-30'")).ShouldBe(0);
    }

    [Fact]
    public async Task ForecastFacts_MarkOnlyMonthsAfterTheReportingMonthAsFuture()
    {
        var future = await factory.ScalarAsync<decimal>("SELECT SUM(amount) FROM vw_forecast_facts WHERE is_future");
        var ftc = await factory.ScalarAsync<decimal>("SELECT SUM(forecast_to_complete) FROM vw_project_financials");

        future.ShouldBe(ftc);
        (await factory.ScalarAsync<long>("SELECT count(*) FROM vw_forecast_facts WHERE is_future AND period_month <= '2026-09-30'")).ShouldBe(0);
    }

    [Fact]
    public async Task DimDate_IsAContinuousCalendarThatContainsTheReportingDate()
    {
        (await factory.ScalarAsync<long>("SELECT count(*) FROM dim_date")).ShouldBe(1095);
        (await factory.ScalarAsync<long>("SELECT count(*) FROM dim_date WHERE date_key = '2026-09-30'")).ShouldBe(1);
    }
}
