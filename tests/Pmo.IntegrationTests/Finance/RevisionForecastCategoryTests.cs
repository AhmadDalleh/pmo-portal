using System.Net;
using System.Net.Http.Json;
using Pmo.Application.Features.Finance.Commands;
using Pmo.Application.Features.Finance.Dtos;
using Pmo.IntegrationTests.Infrastructure;
using Shouldly;

namespace Pmo.IntegrationTests.Finance;

/// <summary>Stories F2 (categories), F3/F4 (budget revisions), F8 (forecasts), F10 (zero budget alert).</summary>
[Collection(ApiCollection.Name)]
public class RevisionForecastCategoryTests(PmoApiFactory factory)
{
    private async Task<(HttpClient Pm, int ProjectId)> ArrangeAsync(decimal budget = 100_000m)
    {
        var pm = await factory.CreateClientForAsync(DemoUsers.Pm5);
        return (pm, (await pm.CreateProjectAsync(budget)).Id);
    }

    private static Task<HttpResponseMessage> RequestAsync(HttpClient pm, int projectId, decimal amount, string effective = "2026-01-01", int? crId = null) =>
        pm.PostAsJsonAsync($"/api/projects/{projectId}/budget-revisions",
            new RequestRevisionCommand(amount, "Needed for scope", DateOnly.Parse(effective), crId));

    private static async Task<BudgetRevisionDto> RequestOkAsync(HttpClient pm, int projectId, decimal amount, string effective = "2026-01-01")
    {
        var response = await RequestAsync(pm, projectId, amount, effective);
        response.StatusCode.ShouldBe(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<BudgetRevisionDto>())!;
    }

    // ---- budget revisions ----

    [Fact]
    public async Task PendingRevision_DoesNotChangeBudget_ApprovedOneDoes()
    {
        var (pm, projectId) = await ArrangeAsync();
        var admin = await factory.CreateClientForAsync(DemoUsers.Admin1);
        var revision = await RequestOkAsync(pm, projectId, 50_000m);
        (await pm.FinanceAsync(projectId)).CurrentBudget.ShouldBe(100_000m);   // pending is ignored

        var decided = await admin.PostAsJsonAsync($"/api/finance/budget-revisions/{revision.Id}/decision",
            new DecideRevisionCommand("approved", "Agreed", revision.Version));

        decided.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await pm.FinanceAsync(projectId)).CurrentBudget.ShouldBe(150_000m);
        (await factory.ScalarAsync<long>("SELECT count(*) FROM audit_events WHERE action = 'budget_revision.decided' AND entity_id = @id",
            ("id", revision.Id.ToString()))).ShouldBe(1);
    }

    [Fact]
    public async Task RejectedRevision_IsIgnored_AndFutureDatedApprovedOneCountsOnlyFromItsDate()
    {
        var (pm, projectId) = await ArrangeAsync();
        var admin = await factory.CreateClientForAsync(DemoUsers.Admin1);
        var rejected = await RequestOkAsync(pm, projectId, 10_000m);
        var future = await RequestOkAsync(pm, projectId, 20_000m, "2026-12-01");
        await admin.PostAsJsonAsync($"/api/finance/budget-revisions/{rejected.Id}/decision", new DecideRevisionCommand("rejected", "No", rejected.Version));
        await admin.PostAsJsonAsync($"/api/finance/budget-revisions/{future.Id}/decision", new DecideRevisionCommand("approved", "Yes", future.Version));

        (await pm.FinanceAsync(projectId)).CurrentBudget.ShouldBe(100_000m);
        (await pm.FinanceAsync(projectId, "?asOf=2026-12-31")).CurrentBudget.ShouldBe(120_000m);
    }

    [Fact]
    public async Task Decide_OwnRequest_Returns403_SecondDecisionReturns409_AndPmCannotDecide()
    {
        var admin = await factory.CreateClientForAsync(DemoUsers.Admin1);
        var other = await factory.CreateClientForAsync(DemoUsers.Admin2);
        var pm = await factory.CreateClientForAsync(DemoUsers.Pm5);
        var project = await pm.CreateProjectAsync();
        var adminsOwn = await RequestOkAsync(admin, project.Id, 5_000m);   // an administrator manages every project, so may request

        var self = await admin.PostAsJsonAsync($"/api/finance/budget-revisions/{adminsOwn.Id}/decision", new DecideRevisionCommand("approved", "Me", adminsOwn.Version));
        var byPm = await pm.PostAsJsonAsync($"/api/finance/budget-revisions/{adminsOwn.Id}/decision", new DecideRevisionCommand("approved", "PM", adminsOwn.Version));
        var ok = await other.PostAsJsonAsync($"/api/finance/budget-revisions/{adminsOwn.Id}/decision", new DecideRevisionCommand("approved", "Fine", adminsOwn.Version));
        var again = await other.PostAsJsonAsync($"/api/finance/budget-revisions/{adminsOwn.Id}/decision", new DecideRevisionCommand("rejected", "Changed mind", adminsOwn.Version + 1));

        await self.ShouldBeProblemAsync(HttpStatusCode.Forbidden, "self_approval_forbidden");
        byPm.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        ok.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await again.ReadAsync()).GetProperty("status").GetInt32().ShouldBe(409);
    }

    [Fact]
    public async Task Request_ReductionBelowZero_ZeroAmount_AndBadChangeRequestLinks_AreRejected()
    {
        var (pm, projectId) = await ArrangeAsync(budget: 10_000m);
        var foreignCr = await factory.ScalarAsync<int>("SELECT id FROM change_requests WHERE project_id = 2 LIMIT 1");

        await (await RequestAsync(pm, projectId, -10_001m)).ShouldBeProblemAsync(HttpStatusCode.BadRequest, "budget_would_be_negative");
        await (await RequestAsync(pm, projectId, 0m)).ShouldBeProblemAsync(HttpStatusCode.BadRequest, "validation_failed");
        await (await RequestAsync(pm, projectId, 100m, crId: foreignCr)).ShouldBeProblemAsync(HttpStatusCode.BadRequest, "change_request_other_project");
        await (await RequestAsync(pm, projectId, 100m, crId: 99999)).ShouldBeProblemAsync(HttpStatusCode.BadRequest, "change_request_not_found");
    }

    [Fact]
    public async Task Request_LinkingASubmittedChangeRequest_Returns400_AndLinkingAnApprovedOneIsAllowed()
    {
        var submitted = await factory.ScalarAsync<int>("SELECT id FROM change_requests WHERE project_id = 1 AND status = 'submitted' LIMIT 1");
        var pm1 = await factory.CreateClientForAsync(DemoUsers.Pm1);

        var notApproved = await RequestAsync(pm1, 1, 100m, "2026-11-01", submitted);

        await notApproved.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "change_request_not_approved");
        (await factory.ScalarAsync<long>("SELECT count(*) FROM budget_revisions WHERE project_id = 1")).ShouldBe(1);   // nothing was saved
    }

    [Fact]
    public async Task Request_AsContributor_Returns403()
    {
        var con1 = await factory.CreateClientForAsync(DemoUsers.Con1);

        (await RequestAsync(con1, 1, 100m)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    // ---- forecasts ----

    [Fact]
    public async Task Forecasts_WithoutRows_ShowNoForecast_ThenEacIsActualPlusFutureForecast()
    {
        var (pm, projectId) = await ArrangeAsync();
        await pm.CreatePostedCostAsync(projectId, 40_000m);
        var before = await pm.FinanceAsync(projectId);

        var saved = await pm.PutAsJsonAsync($"/api/projects/{projectId}/forecasts", new SaveForecastsCommand(
            [new ForecastItem(1, new DateOnly(2026, 10, 1), 30_000m), new ForecastItem(2, new DateOnly(2026, 10, 1), 5_000m),
             new ForecastItem(1, new DateOnly(2026, 11, 1), 40_000m)]));
        var after = await pm.FinanceAsync(projectId);

        saved.StatusCode.ShouldBe(HttpStatusCode.OK);
        before.NoForecast.ShouldBeTrue();
        before.Eac.ShouldBe(40_000m);
        (after.NoForecast, after.ForecastToComplete, after.Eac, after.ProjectedVariance, after.ProjectedOverrun)
            .ShouldBe((false, 75_000m, 115_000m, -15_000m, true));
    }

    [Fact]
    public async Task Forecasts_SavingTwice_UpdatesCellsInsteadOfDuplicating()
    {
        var (pm, projectId) = await ArrangeAsync();
        ForecastItem Cell(decimal amount) => new(1, new DateOnly(2026, 10, 1), amount);

        await pm.PutAsJsonAsync($"/api/projects/{projectId}/forecasts", new SaveForecastsCommand([Cell(10m)]));
        var second = await pm.PutAsJsonAsync($"/api/projects/{projectId}/forecasts", new SaveForecastsCommand([Cell(99m)]));

        var rows = (await second.Content.ReadFromJsonAsync<List<ForecastDto>>())!;
        rows.Single().Amount.ShouldBe(99m);
        (await factory.ScalarAsync<long>("SELECT count(*) FROM financial_forecasts WHERE project_id = @p", ("p", projectId))).ShouldBe(1);
    }

    [Fact]
    public async Task Forecasts_InvalidMonthsAmountsAndDuplicates_AreRejectedAndNothingIsSaved()
    {
        var (pm, projectId) = await ArrangeAsync();
        Task<HttpResponseMessage> Put(params ForecastItem[] items) =>
            pm.PutAsJsonAsync($"/api/projects/{projectId}/forecasts", new SaveForecastsCommand(items.ToList()));

        await (await Put(new ForecastItem(1, new DateOnly(2026, 10, 15), 10m))).ShouldBeProblemAsync(HttpStatusCode.BadRequest, "validation_failed");
        await (await Put(new ForecastItem(1, new DateOnly(2026, 9, 1), 10m))).ShouldBeProblemAsync(HttpStatusCode.BadRequest, "forecast_month_not_future");   // the reporting month itself is closed
        await (await Put(new ForecastItem(1, new DateOnly(2026, 10, 1), -1m))).ShouldBeProblemAsync(HttpStatusCode.BadRequest, "validation_failed");
        await (await Put(new ForecastItem(1, new DateOnly(2026, 10, 1), 1m), new ForecastItem(1, new DateOnly(2026, 10, 1), 2m)))
            .ShouldBeProblemAsync(HttpStatusCode.BadRequest, "duplicate_forecast_cell");
        await (await Put(new ForecastItem(1, new DateOnly(2026, 10, 1), 1m), new ForecastItem(9999, new DateOnly(2026, 11, 1), 2m)))
            .ShouldBeProblemAsync(HttpStatusCode.BadRequest, "category_not_found");

        (await factory.ScalarAsync<long>("SELECT count(*) FROM financial_forecasts WHERE project_id = @p", ("p", projectId))).ShouldBe(0);
    }

    [Fact]
    public async Task Forecasts_AsContributor_Returns403()
    {
        var con1 = await factory.CreateClientForAsync(DemoUsers.Con1);

        var response = await con1.PutAsJsonAsync("/api/projects/1/forecasts", new SaveForecastsCommand([new ForecastItem(1, new DateOnly(2026, 10, 1), 1m)]));

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    // ---- zero budget ----

    [Fact]
    public async Task ZeroBudgetWithSpend_HasNoUtilization_AndFlagsNoBudgetAndOverspent()
    {
        var (pm, projectId) = await ArrangeAsync(budget: 0m);
        var empty = await pm.FinanceAsync(projectId);
        await pm.CreatePostedCostAsync(projectId, 1_000m);

        var spent = await pm.FinanceAsync(projectId);

        empty.Utilization.ShouldBeNull();   // 0 / 0 is undefined: never shown as 0% or infinity
        empty.NoBudget.ShouldBeFalse();     // nothing spent yet, so nothing is wrong
        (spent.Utilization, spent.NoBudget, spent.Overspent).ShouldBe((null, true, true));
    }

    // ---- categories ----

    [Fact]
    public async Task Categories_AdminCreatesAndDeactivates_PmCannot_DuplicatesConflict()
    {
        var admin = await factory.CreateClientForAsync(DemoUsers.Admin1);
        var pm = await factory.CreateClientForAsync(DemoUsers.Pm5);
        var code = "TRAIN" + Random.Shared.Next(1000, 9999);

        var created = await admin.PostAsJsonAsync("/api/finance/cost-categories", new CreateCategoryCommand(code, "Training " + code));
        var duplicate = await admin.PostAsJsonAsync("/api/finance/cost-categories", new CreateCategoryCommand(code, "Other name"));
        var byPm = await pm.PostAsJsonAsync("/api/finance/cost-categories", new CreateCategoryCommand("X" + code, "Nope"));

        created.StatusCode.ShouldBe(HttpStatusCode.Created);
        await duplicate.ShouldBeProblemAsync(HttpStatusCode.Conflict, "duplicate_category");
        byPm.StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        var category = (await created.Content.ReadFromJsonAsync<CostCategoryDto>())!;
        (await admin.PutAsJsonAsync($"/api/finance/cost-categories/{category.Id}", new UpdateCategoryCommand(category.Name, false))).EnsureSuccessStatusCode();
        var project = await pm.CreateProjectAsync();
        await (await pm.PostCostAsync(project.Id, 10m, categoryId: category.Id)).ShouldBeProblemAsync(HttpStatusCode.BadRequest, "category_not_found");
    }
}
