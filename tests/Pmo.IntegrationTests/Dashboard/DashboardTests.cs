using System.Net;
using System.Net.Http.Json;
using Pmo.Application.Common;
using Pmo.Application.Features.Dashboard;
using Pmo.Application.Features.Finance.Dtos;
using Pmo.Application.Features.Tasks.Commands;
using Pmo.IntegrationTests.Infrastructure;
using Shouldly;

namespace Pmo.IntegrationTests.Dashboard;

/// <summary>
/// Epic G and K4 #9-#10. Read-only against the seed, plus one test that changes data through the API and watches the dashboard follow.
/// Expected values are either exact seed figures (filtered to projects only the seed touches) or an independent raw SQL query.
/// </summary>
[Collection(ApiCollection.Name)]
public class DashboardTests(PmoApiFactory factory)
{
    private const string Cutoff = "'2026-09-30'";

    private static async Task<T> GetAsync<T>(HttpClient client, string url) => (await client.GetFromJsonAsync<T>(url))!;

    private Task<int> UserId(string email) => factory.ScalarAsync<int>("SELECT id FROM users WHERE email = @e", ("e", email));

    private Task<HttpClient> AdminAsync() => factory.CreateClientForAsync(DemoUsers.Admin1);

    [Fact]
    public async Task Kpis_Unfiltered_MatchIndependentSqlOverTheWholeDatabase()
    {
        var k = await GetAsync<DashboardKpisDto>(await AdminAsync(), "/api/dashboard/kpis");

        k.AsOf.ShouldBe(new DateOnly(2026, 9, 30));
        k.TotalProjects.ShouldBe((int)await factory.ScalarAsync<long>("SELECT count(*) FROM projects"));
        k.ActualSpend.ShouldBe(await factory.ScalarAsync<decimal>($"SELECT COALESCE(SUM(amount), 0) FROM cost_transactions WHERE status = 'posted' AND txn_date <= {Cutoff}"));
        k.OverdueTasks.ShouldBe((int)await factory.ScalarAsync<long>($"SELECT count(*) FROM tasks WHERE status NOT IN ('done','cancelled') AND due_date < {Cutoff}"));
        k.BlockedTasks.ShouldBe((int)await factory.ScalarAsync<long>("SELECT count(*) FROM tasks WHERE status = 'blocked'"));
        k.OpenHighRisks.ShouldBe((int)await factory.ScalarAsync<long>("SELECT count(*) FROM risks WHERE status IN ('open','mitigating') AND score >= 15"));
        k.PendingChangeRequests.ShouldBe((int)await factory.ScalarAsync<long>("SELECT count(*) FROM change_requests WHERE status = 'submitted'"));
        k.MilestonesDueSoon.ShouldBe((int)await factory.ScalarAsync<long>(
            $"SELECT count(*) FROM milestones WHERE status = 'pending' AND due_date BETWEEN {Cutoff} AND '2026-10-14'"));
        k.PortfolioProgressPct!.Value.ShouldBe(await factory.ScalarAsync<decimal>(
            "SELECT SUM(weight * progress_pct)::numeric / SUM(weight) FROM tasks WHERE status <> 'cancelled'"), 0.0001m);
        k.CurrentBudget.ShouldBe(await factory.ScalarAsync<decimal>(
            $"SELECT SUM(initial_amount) + COALESCE((SELECT SUM(amount) FROM budget_revisions WHERE status = 'approved' AND effective_date <= {Cutoff}), 0) FROM project_budgets"));
        (k.RemainingBudget, k.Eac).ShouldBe((k.CurrentBudget - k.ActualSpend, k.ActualSpend + k.ForecastToComplete));
    }

    [Fact]
    public async Task Kpis_ForOneManager_GiveTheExactSeededFigures()
    {
        var pm1 = await UserId(DemoUsers.Pm1);   // owns PRJ-001, PRJ-006, PRJ-009 and nothing created by tests

        var k = await GetAsync<DashboardKpisDto>(await AdminAsync(), $"/api/dashboard/kpis?managerId={pm1}");

        (k.TotalProjects, k.ActiveProjects).ShouldBe((3, 2));
        (k.CurrentBudget, k.ActualSpend, k.RemainingBudget).ShouldBe((2_850_000m, 2_175_500m, 674_500m));
        (k.ForecastToComplete, k.Eac, k.ProjectedVariance).ShouldBe((350_000m, 2_525_500m, 324_500m));
        (k.ProjectedOverrunProjects, k.ProjectedOverrunAmount).ShouldBe((1, 109_500m));   // only PRJ-009
        k.Utilization!.Value.ShouldBe(2_175_500m / 2_850_000m, 0.00001m);
    }

    [Fact]
    public async Task Kpis_AsContributor_OnlyCountProjectsTheyBelongTo()
    {
        var con1 = await factory.CreateClientForAsync(DemoUsers.Con1);

        var k = await GetAsync<DashboardKpisDto>(con1, "/api/dashboard/kpis");

        var member = await factory.ScalarAsync<long>("SELECT count(*) FROM project_members WHERE user_id = (SELECT id FROM users WHERE email = 'con1@pmo.example')");
        k.TotalProjects.ShouldBe((int)member);
        k.ActualSpend.ShouldBe(await factory.ScalarAsync<decimal>(
            $"SELECT COALESCE(SUM(amount), 0) FROM cost_transactions c WHERE status = 'posted' AND txn_date <= {Cutoff} AND project_id IN (SELECT project_id FROM project_members WHERE user_id = (SELECT id FROM users WHERE email = 'con1@pmo.example'))"));
    }

    [Fact]
    public async Task Filters_NarrowTheProjectSet_AndAnOutOfScopeProjectIdYieldsNothing()
    {
        var admin = await AdminAsync();
        var con1 = await factory.CreateClientForAsync(DemoUsers.Con1);

        (await GetAsync<DashboardKpisDto>(admin, "/api/dashboard/kpis?status=on_hold")).TotalProjects.ShouldBe(1);
        (await GetAsync<DashboardKpisDto>(admin, "/api/dashboard/kpis?projectId=3")).TotalProjects.ShouldBe(1);
        (await GetAsync<DashboardKpisDto>(admin, "/api/dashboard/kpis?departmentId=3")).TotalProjects
            .ShouldBe((int)await factory.ScalarAsync<long>("SELECT count(*) FROM projects WHERE department_id = 3"));
        (await GetAsync<DashboardKpisDto>(admin, "/api/dashboard/kpis?from=2027-06-01")).TotalProjects
            .ShouldBe((int)await factory.ScalarAsync<long>("SELECT count(*) FROM projects WHERE planned_end_date >= '2027-06-01'"));
        var hidden = await GetAsync<DashboardKpisDto>(con1, "/api/dashboard/kpis?projectId=2");   // PRJ-002: not a member
        (hidden.TotalProjects, hidden.ActualSpend, hidden.OverdueTasks).ShouldBe((0, 0m, 0));
    }

    [Fact]
    public async Task Filters_InvalidStatusOrBackwardsTimeframe_Return400()
    {
        var admin = await AdminAsync();

        await (await admin.GetAsync("/api/dashboard/kpis?status=lost")).ShouldBeProblemAsync(HttpStatusCode.BadRequest, "validation_failed");
        await (await admin.GetAsync("/api/dashboard/kpis?from=2026-12-01&to=2026-01-01")).ShouldBeProblemAsync(HttpStatusCode.BadRequest, "validation_failed");
    }

    [Fact]
    public async Task Dashboard_WithoutToken_Returns401()
    {
        (await factory.CreateClient().GetAsync("/api/dashboard/kpis")).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Theory]
    [InlineData("")]
    [InlineData("?status=active")]
    [InlineData("?departmentId=2")]
    [InlineData("?from=2026-01-01&to=2026-12-31")]
    [InlineData("?asOf=2026-10-31")]
    public async Task CostDrilldown_SumsToTheActualSpendKpi_ForEveryFilter(string query)
    {
        var admin = await AdminAsync();

        var kpi = await GetAsync<DashboardKpisDto>(admin, "/api/dashboard/kpis" + query);
        var drill = await GetAsync<DrilldownCostsResult>(admin, "/api/dashboard/drilldown/costs" + query + (query == "" ? "?pageSize=5" : "&pageSize=5"));

        drill.TotalAmount.ShouldBe(kpi.ActualSpend);
        drill.Items.Count.ShouldBeLessThanOrEqualTo(5);
        drill.TotalCount.ShouldBeGreaterThan(0);
    }

    [Fact]
    public async Task CostDrilldown_ListsOnlyPostedRowsUpToTheCutoff_AndPagesThroughAllOfThem()
    {
        var admin = await AdminAsync();
        var pm1 = await UserId(DemoUsers.Pm1);
        var first = await GetAsync<DrilldownCostsResult>(admin, $"/api/dashboard/drilldown/costs?managerId={pm1}&pageSize=10&page=1");
        var all = new List<DrilldownCostDto>(first.Items);
        for (var page = 2; all.Count < first.TotalCount; page++)
        {
            all.AddRange((await GetAsync<DrilldownCostsResult>(admin, $"/api/dashboard/drilldown/costs?managerId={pm1}&pageSize=10&page={page}")).Items);
        }

        all.Count.ShouldBe(first.TotalCount);
        all.Sum(c => c.Amount).ShouldBe(first.TotalAmount);
        all.ShouldAllBe(c => c.TxnDate <= new DateOnly(2026, 9, 30));
        all.Select(c => c.Id).Distinct().Count().ShouldBe(all.Count);
        all.Select(c => c.ProjectCode).Distinct().Order().ShouldBe(["PRJ-001", "PRJ-006", "PRJ-009"]);
    }

    [Fact]
    public async Task CategoryFilter_AppliesToFinanceVisualsAndCostDrilldown_ButNotToTheKpiCards()
    {
        var admin = await AdminAsync();
        var software = await factory.ScalarAsync<int>("SELECT id FROM cost_categories WHERE code = 'SOFTWARE'");
        var f = $"?categoryId={software}";
        var kpi = await GetAsync<DashboardKpisDto>(admin, "/api/dashboard/kpis");
        var kpiWithCategory = await GetAsync<DashboardKpisDto>(admin, "/api/dashboard/kpis" + f);

        var drill = await GetAsync<DrilldownCostsResult>(admin, "/api/dashboard/drilldown/costs" + f);
        var byCategory = await GetAsync<List<CategorySpendDto>>(admin, "/api/dashboard/finance/by-category" + f);
        var monthly = await GetAsync<List<MonthlyPointDto>>(admin, "/api/dashboard/finance/monthly" + f);

        kpiWithCategory.ActualSpend.ShouldBe(kpi.ActualSpend);
        byCategory.Single().CategoryId.ShouldBe(software);
        drill.TotalAmount.ShouldBe(byCategory.Single().ActualSpend);
        monthly.Sum(m => m.ActualSpend).ShouldBe(drill.TotalAmount);
        drill.TotalAmount.ShouldBeLessThan(kpi.ActualSpend);
    }

    [Fact]
    public async Task FinanceCharts_AddUpToTheKpis()
    {
        var admin = await AdminAsync();
        var kpi = await GetAsync<DashboardKpisDto>(admin, "/api/dashboard/kpis");

        var byProject = await GetAsync<List<ProjectFinanceBarDto>>(admin, "/api/dashboard/finance/by-project");
        var monthly = await GetAsync<List<MonthlyPointDto>>(admin, "/api/dashboard/finance/monthly");
        var byCategory = await GetAsync<List<CategorySpendDto>>(admin, "/api/dashboard/finance/by-category");

        byProject.Sum(p => p.ActualSpend).ShouldBe(kpi.ActualSpend);
        byProject.Sum(p => p.CurrentBudget).ShouldBe(kpi.CurrentBudget);
        byProject.Sum(p => p.Eac).ShouldBe(kpi.Eac);
        monthly.Sum(m => m.ActualSpend).ShouldBe(kpi.ActualSpend);
        monthly.Sum(m => m.Forecast).ShouldBe(kpi.ForecastToComplete);
        byCategory.Sum(c => c.ActualSpend).ShouldBe(kpi.ActualSpend);
        byCategory.Sum(c => c.ForecastToComplete).ShouldBe(kpi.ForecastToComplete);
        byProject.Sum(p => p.ActualSpend).ShouldBe(await factory.ScalarAsync<decimal>(   // K4 #9: equals the plain SUM, no join fan-out
            $"SELECT SUM(amount) FROM cost_transactions WHERE status = 'posted' AND txn_date <= {Cutoff}"));
    }

    [Fact]
    public async Task BudgetVsActualVsEac_ForOneManager_ShowsTheOverspentProject()
    {
        var pm1 = await UserId(DemoUsers.Pm1);

        var bars = await GetAsync<List<ProjectFinanceBarDto>>(await AdminAsync(), $"/api/dashboard/finance/by-project?managerId={pm1}");

        bars.Select(b => b.Code).ShouldBe(["PRJ-001", "PRJ-006", "PRJ-009"]);
        var chatbot = bars.Single(b => b.Code == "PRJ-009");
        (chatbot.CurrentBudget, chatbot.ActualSpend, chatbot.Eac).ShouldBe((550_000m, 599_500m, 659_500m));
    }

    [Fact]
    public async Task OverdueTasks_KpiBarsAndDrilldownAgree()
    {
        var admin = await AdminAsync();
        var kpi = await GetAsync<DashboardKpisDto>(admin, "/api/dashboard/kpis");

        var bars = await GetAsync<List<OverdueByOwnerDto>>(admin, "/api/dashboard/delivery/overdue-by-owner");
        var drill = await GetAsync<PagedResult<DrilldownTaskDto>>(admin, "/api/dashboard/drilldown/tasks?pageSize=100");

        bars.Sum(b => b.OverdueTasks).ShouldBe(kpi.OverdueTasks);
        drill.TotalCount.ShouldBe(kpi.OverdueTasks);
        drill.Items.ShouldAllBe(t => t.DaysOverdue > 0 && t.DueDate < new DateOnly(2026, 9, 30) && t.Status != "done");
        var top = bars.First(b => b.UserId != null);
        (await GetAsync<PagedResult<DrilldownTaskDto>>(admin, $"/api/dashboard/drilldown/tasks?assigneeId={top.UserId}")).TotalCount.ShouldBe(top.OverdueTasks);
        var unassigned = bars.FirstOrDefault(b => b.UserId == null);
        if (unassigned is not null)
        {
            (await GetAsync<PagedResult<DrilldownTaskDto>>(admin, "/api/dashboard/drilldown/tasks?unassigned=true")).TotalCount.ShouldBe(unassigned.OverdueTasks);
        }
    }

    [Fact]
    public async Task TaskDrilldown_WithOverdueFalse_ListsAllOpenTasks()
    {
        var admin = await AdminAsync();

        var open = await GetAsync<PagedResult<DrilldownTaskDto>>(admin, "/api/dashboard/drilldown/tasks?overdue=false&projectId=7&pageSize=100");

        open.TotalCount.ShouldBe((int)await factory.ScalarAsync<long>("SELECT count(*) FROM tasks WHERE project_id = 7 AND status NOT IN ('done','cancelled')"));
        open.Items.ShouldContain(t => t.DaysOverdue == 0);   // the task due exactly on the as-of date is open but not overdue
    }

    [Fact]
    public async Task RiskMatrix_CountsOpenItemsPerCell_AndEachCellDrillsDownToItsRisks()
    {
        var admin = await AdminAsync();

        var matrix = await GetAsync<List<RiskMatrixCellDto>>(admin, "/api/dashboard/risks/matrix");
        var kpi = await GetAsync<DashboardKpisDto>(admin, "/api/dashboard/kpis");

        matrix.Count.ShouldBe(25);
        matrix.Sum(c => c.OpenCount).ShouldBe((int)await factory.ScalarAsync<long>("SELECT count(*) FROM risks WHERE status IN ('open','mitigating')"));
        var worst = matrix.Single(c => c is { Probability: 5, Impact: 5 });
        worst.OpenCount.ShouldBeGreaterThanOrEqualTo(1);
        var cell = await GetAsync<PagedResult<DrilldownRiskDto>>(admin, "/api/dashboard/drilldown/risks?probability=5&impact=5");
        cell.TotalCount.ShouldBe(worst.OpenCount);
        cell.Items.ShouldAllBe(r => r.Probability == 5 && r.Impact == 5 && r.Status != "closed");
        (await GetAsync<PagedResult<DrilldownRiskDto>>(admin, "/api/dashboard/drilldown/risks?highExposure=true")).TotalCount.ShouldBe(kpi.OpenHighRisks);
    }

    [Fact]
    public async Task ProjectHealth_UsesTheSameBadgeRuleAsTheProjectPage()
    {
        var pm1 = await UserId(DemoUsers.Pm1);
        var admin = await AdminAsync();

        var rows = await GetAsync<List<ProjectHealthRowDto>>(admin, $"/api/dashboard/delivery/project-health?managerId={pm1}");

        rows.Select(r => r.Code).ShouldBe(["PRJ-001", "PRJ-006", "PRJ-009"]);
        rows.Single(r => r.Code == "PRJ-009").Health.ShouldBe("red");
        rows.Single(r => r.Code == "PRJ-001").Health.ShouldBe("amber");
        foreach (var row in rows)
        {
            var page = await GetAsync<Pmo.Application.Features.Projects.Dtos.ProjectSummaryDto>(admin, $"/api/projects/{row.ProjectId}/summary");
            page.Health.ShouldBe(row.Health);
        }
    }

    [Fact]
    public async Task Dashboard_FollowsDataChangesOnTheNextRead_WithNoClientSidePatching()
    {
        var pm = await factory.CreateClientForAsync(DemoUsers.Pm5);
        var project = await pm.CreateProjectAsync(budget: 100_000m);
        await pm.AddMemberAsync(project.Id, await UserId(DemoUsers.Con1));
        var con = await factory.CreateClientForAsync(DemoUsers.Con1);
        var admin = await AdminAsync();
        var overdue = await pm.CreateTaskAsync(project.Id, ApiTestHelpers.NewTask("late", await UserId(DemoUsers.Con1), due: new DateOnly(2026, 8, 1)));
        string Filter(string path) => $"/api/dashboard/{path}?projectId={project.Id}";

        (await GetAsync<DashboardKpisDto>(admin, Filter("kpis"))).OverdueTasks.ShouldBe(1);
        await con.PatchAsJsonAsync($"/api/tasks/{overdue.Id}/progress", new UpdateTaskProgressCommand(100, "done", overdue.Version));
        var afterTask = await GetAsync<DashboardKpisDto>(admin, Filter("kpis"));
        await pm.CreatePostedCostAsync(project.Id, 10_000m);
        var afterCost = await GetAsync<DashboardKpisDto>(admin, Filter("kpis"));
        var drill = await GetAsync<DrilldownCostsResult>(admin, Filter("drilldown/costs"));

        (afterTask.OverdueTasks, afterTask.PortfolioProgressPct).ShouldBe((0, 100m));
        (afterCost.ActualSpend, afterCost.RemainingBudget, afterCost.Utilization).ShouldBe((10_000m, 90_000m, 0.10m));
        drill.TotalAmount.ShouldBe(10_000m);
    }
}
