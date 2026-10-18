using System.Net.Http.Json;
using Pmo.Application.Common;
using Pmo.Application.Features.Finance.Dtos;
using Pmo.IntegrationTests.Infrastructure;
using Shouldly;

namespace Pmo.IntegrationTests.Finance;

/// <summary>F1, F9 and reconciliation checks against the seeded projects (ids 1-10). Read-only.</summary>
[Collection(ApiCollection.Name)]
public class FinanceReadTests(PmoApiFactory factory)
{
    [Fact]
    public async Task Summary_BreaksTheBudgetDownIntoInitialRevisionsAndCurrent()
    {
        var admin = await factory.CreateClientForAsync(DemoUsers.Admin1);

        var s = await admin.FinanceAsync(1);

        (s.InitialBudget, s.ApprovedRevisions, s.CurrentBudget).ShouldBe((1_200_000m, 100_000m, 1_300_000m));
        (s.ActualSpend, s.RemainingBudget).ShouldBe((1_196_000m, 104_000m));
        s.Currency.ShouldBe("AED");
        s.PendingRevisions.ShouldBe(0);
    }

    [Fact]
    public async Task Summary_ProjectWithPendingRevision_CountsItButIgnoresItsAmount()
    {
        var s = await (await factory.CreateClientForAsync(DemoUsers.Admin1)).FinanceAsync(8);

        s.PendingRevisions.ShouldBe(1);
        s.CurrentBudget.ShouldBe(1_950_000m);
    }

    [Fact]
    public async Task Summary_WithAndWithoutForecast_FollowsEacDefinition()
    {
        var admin = await factory.CreateClientForAsync(DemoUsers.Admin1);

        var withForecast = await admin.FinanceAsync(7);
        var withoutForecast = await admin.FinanceAsync(5);

        withForecast.Eac.ShouldBe(withForecast.ActualSpend + withForecast.ForecastToComplete);
        withForecast.ProjectedVariance.ShouldBe(withForecast.CurrentBudget - withForecast.Eac);
        withForecast.NoForecast.ShouldBeFalse();
        withoutForecast.NoForecast.ShouldBeTrue();
        withoutForecast.Eac.ShouldBe(withoutForecast.ActualSpend);
    }

    [Fact]
    public async Task ByCategoryAndMonthly_AddUpToTheSummaryFigures()
    {
        var admin = await factory.CreateClientForAsync(DemoUsers.Admin1);
        var summary = await admin.FinanceAsync(2);

        var categories = await admin.GetFromJsonAsync<List<CategorySpendDto>>("/api/projects/2/finance/by-category");
        var months = await admin.GetFromJsonAsync<List<MonthlyPointDto>>("/api/projects/2/finance/monthly");

        categories!.Sum(c => c.ActualSpend).ShouldBe(summary.ActualSpend);
        categories.Sum(c => c.ForecastToComplete).ShouldBe(summary.ForecastToComplete);
        months!.Sum(m => m.ActualSpend).ShouldBe(summary.ActualSpend);
        months.Sum(m => m.Forecast).ShouldBe(summary.ForecastToComplete);
        months.Select(m => m.Month).ShouldBe(months.Select(m => m.Month).Order());
    }

    [Fact]
    public async Task Monthly_CategoryFilter_OnlyCountsThatCategory()
    {
        var admin = await factory.CreateClientForAsync(DemoUsers.Admin1);
        var labour = await factory.ScalarAsync<int>("SELECT id FROM cost_categories WHERE code = 'LABOUR'");
        var expected = await factory.ScalarAsync<decimal>(
            "SELECT COALESCE(SUM(amount), 0) FROM cost_transactions WHERE project_id = 2 AND status = 'posted' AND txn_date <= '2026-09-30' AND category_id = @c", ("c", labour));

        var months = await admin.GetFromJsonAsync<List<MonthlyPointDto>>($"/api/projects/2/finance/monthly?categoryId={labour}");

        months!.Sum(m => m.ActualSpend).ShouldBe(expected);
    }

    [Fact]
    public async Task PortfolioSpend_EqualsPlainSumOverCostTransactions_NoJoinFanOut()
    {
        var admin = await factory.CreateClientForAsync(DemoUsers.Admin1);
        var total = 0m;
        for (var id = 1; id <= 10; id++)
        {
            total += (await admin.FinanceAsync(id)).ActualSpend;
        }

        var raw = await factory.ScalarAsync<decimal>(
            "SELECT SUM(amount) FROM cost_transactions WHERE status = 'posted' AND txn_date <= '2026-09-30' AND project_id <= 10");

        total.ShouldBe(raw);   // tasks, risks and forecasts also exist per project; none of them inflates spend
    }

    [Fact]
    public async Task CostList_FiltersByStatus_AndOutOfScopeIs404()
    {
        var admin = await factory.CreateClientForAsync(DemoUsers.Admin1);
        var con1 = await factory.CreateClientForAsync(DemoUsers.Con1);

        var drafts = await admin.GetFromJsonAsync<PagedResult<CostDto>>("/api/projects/1/costs?status=draft");
        var visibleToMember = await con1.GetAsync("/api/projects/1/costs");
        var hidden = await con1.GetAsync("/api/projects/2/costs");

        drafts!.TotalCount.ShouldBe(2);
        drafts.Items.ShouldAllBe(c => c.Status == "draft" && c.PostedAt == null);
        visibleToMember.StatusCode.ShouldBe(System.Net.HttpStatusCode.OK);
        hidden.StatusCode.ShouldBe(System.Net.HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task CostList_ShowsTheSeededReversalAsLinkedAndOriginalAsReversed()
    {
        var admin = await factory.CreateClientForAsync(DemoUsers.Admin1);

        var page = await admin.GetFromJsonAsync<PagedResult<CostDto>>("/api/projects/2/costs?pageSize=100");

        var reversal = page!.Items.Single(c => c.ReversesTransactionId != null);
        reversal.Amount.ShouldBeLessThan(0);
        page.Items.Single(c => c.Id == reversal.ReversesTransactionId).IsReversed.ShouldBeTrue();
    }
}
