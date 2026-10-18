using System.Net;
using System.Net.Http.Json;
using Pmo.Application.Common;
using Pmo.Application.Features.Projects.Dtos;
using Pmo.IntegrationTests.Infrastructure;
using Shouldly;

namespace Pmo.IntegrationTests.Projects;

/// <summary>Stories B3 and B4 and scope (A3), against the seeded data as of 2026-09-30. Read-only.</summary>
[Collection(ApiCollection.Name)]
public class ProjectReadTests(PmoApiFactory factory)
{
    private static async Task<List<ProjectListItemDto>> ListAsync(HttpClient client, string query = "")
    {
        var page = await client.GetFromJsonAsync<PagedResult<ProjectListItemDto>>("/api/projects" + query);
        return page!.Items.ToList();
    }

    private async Task<ProjectSummaryDto> SummaryOfAsync(string code)
    {
        var admin = await factory.CreateClientForAsync(DemoUsers.Admin1);
        var all = await ListAsync(admin, $"?search={code}");
        return (await admin.GetFromJsonAsync<ProjectSummaryDto>($"/api/projects/{all.Single(p => p.Code == code).Id}/summary"))!;
    }

    [Fact]
    public async Task List_AsAdministrator_ReturnsAllTenProjects()
    {
        var items = await ListAsync(await factory.CreateClientForAsync(DemoUsers.Admin1), "?search=PRJ-");   // other tests create projects with other codes

        items.Count.ShouldBe(10);
        items.Select(p => p.Code).ShouldBe(Enumerable.Range(1, 10).Select(i => $"PRJ-{i:D3}"));   // default sort: code
    }

    [Fact]
    public async Task List_AsContributor_ReturnsOnlyMemberProjects()
    {
        var items = await ListAsync(await factory.CreateClientForAsync(DemoUsers.Con1), "?search=PRJ-");

        items.Select(p => p.Code).ShouldBe(["PRJ-001", "PRJ-004", "PRJ-007", "PRJ-008"]);
    }

    [Fact]
    public async Task List_FilterByStatusAndSearch_NarrowsResults()
    {
        var admin = await factory.CreateClientForAsync(DemoUsers.Admin1);

        (await ListAsync(admin, "?status=on_hold")).Select(p => p.Code).ShouldBe(["PRJ-006"]);
        (await ListAsync(admin, "?search=chatbot")).Select(p => p.Code).ShouldBe(["PRJ-009"]);
        (await ListAsync(admin, "?search=prj-00")).Count.ShouldBe(9);   // PRJ-001..009, matching the code
    }

    [Fact]
    public async Task List_SortedByUtilizationDescending_PutsOverspentProjectFirst()
    {
        var items = await ListAsync(await factory.CreateClientForAsync(DemoUsers.Admin1), "?sort=utilization&dir=desc");

        items[0].Code.ShouldBe("PRJ-009");
        items[0].Utilization!.Value.ShouldBe(1.09m, 0.001m);
        items.Skip(items.Count - 2).All(p => p.Utilization == 0m).ShouldBeTrue();   // PRJ-003 and PRJ-010 have no posted spend
    }

    [Fact]
    public async Task List_Paging_ReturnsRequestedPageAndTotal()
    {
        var admin = await factory.CreateClientForAsync(DemoUsers.Admin1);

        var page = await admin.GetFromJsonAsync<PagedResult<ProjectListItemDto>>("/api/projects?search=PRJ-&page=2&pageSize=4");

        page!.Items.Select(p => p.Code).ShouldBe(["PRJ-005", "PRJ-006", "PRJ-007", "PRJ-008"]);
        page.TotalCount.ShouldBe(10);
    }

    [Fact]
    public async Task List_UnknownSortField_Returns400WithFieldError()
    {
        var response = await (await factory.CreateClientForAsync(DemoUsers.Admin1)).GetAsync("/api/projects?sort=salary");

        await response.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "validation_failed");
        (await response.FieldErrorsAsync("sort")).Single().ShouldContain("Sort must be one of");
    }

    [Fact]
    public async Task Get_ProjectOutsideScope_Returns404()
    {
        var con1 = await factory.CreateClientForAsync(DemoUsers.Con1);   // not a member of PRJ-002 (id 2)

        var response = await con1.GetAsync("/api/projects/2");

        await response.ShouldBeProblemAsync(HttpStatusCode.NotFound, "not_found");
    }

    [Fact]
    public async Task Get_ProjectThatDoesNotExist_Returns404LikeOutOfScope()
    {
        var response = await (await factory.CreateClientForAsync(DemoUsers.Admin1)).GetAsync("/api/projects/9999");

        await response.ShouldBeProblemAsync(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Get_ProjectInScope_ReturnsDetailsWithVersion()
    {
        var project = await (await factory.CreateClientForAsync(DemoUsers.Con1)).GetFromJsonAsync<ProjectDto>("/api/projects/1");

        project!.Code.ShouldBe("PRJ-001");
        project.DepartmentName.ShouldBe("Digital Channels");
        project.InitialBudget.ShouldBe(1_200_000m);
        project.Version.ShouldBeGreaterThan(0u);
    }

    [Fact]
    public async Task Summary_NearBudgetProject_MatchesSeededFinancials()
    {
        var s = await SummaryOfAsync("PRJ-001");

        s.AsOf.ShouldBe(new DateOnly(2026, 9, 30));
        s.Finance.CurrentBudget.ShouldBe(1_300_000m);          // 1.2M + approved revision 100k (effective 2026-03-01)
        s.Finance.ActualSpend.ShouldBe(1_196_000m);
        s.Finance.RemainingBudget.ShouldBe(104_000m);
        s.Finance.Utilization!.Value.ShouldBe(0.92m, 0.0001m);
        s.Finance.ForecastToComplete.ShouldBe(90_000m);
        s.Finance.Eac.ShouldBe(1_286_000m);
        s.Finance.ProjectedVariance.ShouldBe(14_000m);
        s.Finance.NearBudget.ShouldBeTrue();
        s.Finance.Overspent.ShouldBeFalse();
        s.Governance.OpenHighExposureRisks.ShouldBe(1);
        s.Delivery.OverdueTasks.ShouldBe(1);
        s.Delivery.NextMilestone!.DueDate.ShouldBe(new DateOnly(2026, 10, 10));
        s.Health.ShouldBe("amber");
    }

    [Fact]
    public async Task Summary_OverspentProject_ExcludesPostCutoffCostAndIsRed()
    {
        var s = await SummaryOfAsync("PRJ-009");

        s.Finance.ActualSpend.ShouldBe(599_500m);   // the posted cost dated 2026-10-05 is not counted
        s.Finance.CurrentBudget.ShouldBe(550_000m);
        s.Finance.Overspent.ShouldBeTrue();
        s.Finance.ProjectedOverrun.ShouldBeTrue();
        s.Health.ShouldBe("red");
    }

    [Fact]
    public async Task Summary_ForecastOverrun_UsesEacAboveBudget()
    {
        var s = await SummaryOfAsync("PRJ-002");

        s.Finance.Eac.ShouldBe(2_947_000m);
        s.Finance.ProjectedVariance.ShouldBe(-247_000m);
        s.Finance.ProjectedOverrun.ShouldBeTrue();
        s.Finance.Overspent.ShouldBeFalse();
    }

    [Fact]
    public async Task Summary_DraftOnlySpend_IsZero()
    {
        var s = await SummaryOfAsync("PRJ-003");

        s.Finance.ActualSpend.ShouldBe(0m);
        s.Finance.Utilization.ShouldBe(0m);
        s.Finance.RemainingBudget.ShouldBe(800_000m);
    }

    [Fact]
    public async Task Summary_NoForecastRows_TreatsForecastAsZeroAndFlagsIt()
    {
        var s = await SummaryOfAsync("PRJ-005");

        s.Finance.NoForecast.ShouldBeTrue();
        s.Finance.ForecastToComplete.ShouldBe(0m);
        s.Finance.Eac.ShouldBe(s.Finance.ActualSpend);   // we never invent a forecast
        s.Finance.CurrentBudget.ShouldBe(620_000m);      // 650k less a 30k approved reduction
    }

    [Fact]
    public async Task Summary_PendingRejectedAndFutureRevisions_AreIgnoredInCurrentBudget()
    {
        (await SummaryOfAsync("PRJ-008")).Finance.CurrentBudget.ShouldBe(1_950_000m);   // pending +100k ignored
        (await SummaryOfAsync("PRJ-007")).Finance.CurrentBudget.ShouldBe(1_500_000m);   // rejected -50k and approved-but-future +100k ignored
    }

    [Fact]
    public async Task Summary_ProjectWithoutTasks_HasNoProgressValue()
    {
        var s = await SummaryOfAsync("PRJ-010");

        s.Delivery.ProgressPct.ShouldBeNull();   // shown as "-" in the UI, never 0%
    }

    [Fact]
    public async Task Summary_AsOfParameter_ChangesTheCutoff()
    {
        var admin = await factory.CreateClientForAsync(DemoUsers.Admin1);

        var s = await admin.GetFromJsonAsync<ProjectSummaryDto>("/api/projects/9/summary?asOf=2026-10-31");

        s!.Finance.ActualSpend.ShouldBe(629_500m);   // 599,500 + the 30,000 invoice dated 2026-10-05
    }

    [Fact]
    public async Task Summary_PortfolioProgressMatchesIndependentSql()
    {
        var s = await SummaryOfAsync("PRJ-001");
        var expected = await factory.ScalarAsync<decimal>(
            "SELECT SUM(weight * progress_pct)::numeric / SUM(weight) FROM tasks WHERE project_id = 1 AND status <> 'cancelled'");

        s.Delivery.ProgressPct!.Value.ShouldBe(expected, 0.0001m);
    }
}
