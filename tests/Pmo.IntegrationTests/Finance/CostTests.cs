using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Pmo.Application.Features.Finance.Commands;
using Pmo.Application.Features.Finance.Dtos;
using Pmo.IntegrationTests.Infrastructure;
using Shouldly;

namespace Pmo.IntegrationTests.Finance;

/// <summary>Stories F5, F6, F7. Each test works on its own project (budget 100,000) so numbers are exact.</summary>
[Collection(ApiCollection.Name)]
public class CostTests(PmoApiFactory factory)
{
    private Task<int> UserId(string email) => factory.ScalarAsync<int>("SELECT id FROM users WHERE email = @e", ("e", email));

    private async Task<(HttpClient Pm, int ProjectId)> ArrangeAsync(decimal budget = 100_000m)
    {
        var pm = await factory.CreateClientForAsync(DemoUsers.Pm5);
        return (pm, (await pm.CreateProjectAsync(budget)).Id);
    }

    [Fact]
    public async Task CreateDraft_PersistsDraftAndDoesNotCountAsSpend()
    {
        var (pm, projectId) = await ArrangeAsync();

        var draft = await pm.CreateCostAsync(projectId, 1_000m);

        draft.Status.ShouldBe("draft");
        (await factory.ScalarAsync<string>("SELECT status FROM cost_transactions WHERE id = @id", ("id", draft.Id))).ShouldBe("draft");
        (await pm.FinanceAsync(projectId)).ActualSpend.ShouldBe(0m);
    }

    [Fact]
    public async Task CreateDraft_SameIdempotencyKeyTwice_ReturnsTheOriginalAndNoDuplicate()
    {
        var (pm, projectId) = await ArrangeAsync();
        var key = Guid.NewGuid();

        var first = await pm.PostCostAsync(projectId, 1_000m, key: key);
        var second = await pm.PostCostAsync(projectId, 1_000m, key: key);

        first.StatusCode.ShouldBe(HttpStatusCode.Created);
        first.Headers.Location.ShouldNotBeNull();
        second.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await second.Content.ReadFromJsonAsync<CostDto>())!.Id.ShouldBe((await first.Content.ReadFromJsonAsync<CostDto>())!.Id);
        (await factory.ScalarAsync<long>("SELECT count(*) FROM cost_transactions WHERE idempotency_key = @k", ("k", key))).ShouldBe(1);
    }

    [Fact]
    public async Task CreateDraft_KeyAlreadyUsedForAnotherProject_Returns409()
    {
        var (pm, projectId) = await ArrangeAsync();
        var otherProject = (await pm.CreateProjectAsync()).Id;
        var key = Guid.NewGuid();
        (await pm.PostCostAsync(projectId, 10m, key: key)).EnsureSuccessStatusCode();

        var response = await pm.PostCostAsync(otherProject, 10m, key: key);

        await response.ShouldBeProblemAsync(HttpStatusCode.Conflict, "idempotency_key_reused");
    }

    [Fact]
    public async Task CreateDraft_WithoutKey_ZeroAmount_OrInactiveCategory_IsRejected()
    {
        var (pm, projectId) = await ArrangeAsync();
        var noKey = await pm.PostAsJsonAsync($"/api/projects/{projectId}/costs", new CreateCostCommand(1, new DateOnly(2026, 8, 1), 10m, "x", null, null));
        var zero = await pm.PostCostAsync(projectId, 0m);
        var unknownCategory = await pm.PostCostAsync(projectId, 10m, categoryId: 9999);

        await noKey.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "idempotency_key_required");
        await zero.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "validation_failed");
        (await zero.FieldErrorsAsync("amount")).ShouldNotBeEmpty();
        await unknownCategory.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "category_not_found");
    }

    [Fact]
    public async Task CreateDraft_AsContributorMember_Returns403()
    {
        var (pm, projectId) = await ArrangeAsync();
        await pm.AddMemberAsync(projectId, await UserId(DemoUsers.Con1));
        var con1 = await factory.CreateClientForAsync(DemoUsers.Con1);

        var response = await con1.PostCostAsync(projectId, 10m);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await factory.ScalarAsync<long>("SELECT count(*) FROM cost_transactions WHERE project_id = @p", ("p", projectId))).ShouldBe(0);
    }

    [Fact]
    public async Task UpdateDraft_ChangesFields_RejectsStaleVersion_AndRefusesPosted()
    {
        var (pm, projectId) = await ArrangeAsync();
        var draft = await pm.CreateCostAsync(projectId, 1_000m);
        UpdateCostCommand Edit(decimal amount, uint version) => new(1, new DateOnly(2026, 8, 2), amount, "Edited", null, null, version);

        var ok = await pm.PutAsJsonAsync($"/api/finance/costs/{draft.Id}", Edit(2_000m, draft.Version));
        var stale = await pm.PutAsJsonAsync($"/api/finance/costs/{draft.Id}", Edit(3_000m, draft.Version));
        var posted = await pm.CreatePostedCostAsync(projectId, 500m);
        var afterPosting = await pm.PutAsJsonAsync($"/api/finance/costs/{posted.Id}", Edit(1m, posted.Version));

        ok.StatusCode.ShouldBe(HttpStatusCode.OK);
        await stale.ShouldBeProblemAsync(HttpStatusCode.Conflict, "concurrency_conflict");
        await afterPosting.ShouldBeProblemAsync(HttpStatusCode.Conflict, "posted_immutable");
        (await factory.ScalarAsync<decimal>("SELECT amount FROM cost_transactions WHERE id = @id", ("id", draft.Id))).ShouldBe(2_000m);
    }

    [Fact]
    public async Task DeleteDraft_Works_ButPostedCostsAreNeverDeleted()
    {
        var (pm, projectId) = await ArrangeAsync();
        var draft = await pm.CreateCostAsync(projectId, 100m);
        var posted = await pm.CreatePostedCostAsync(projectId, 100m);

        var deleted = await pm.DeleteAsync($"/api/finance/costs/{draft.Id}");
        var refused = await pm.DeleteAsync($"/api/finance/costs/{posted.Id}");

        deleted.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        await refused.ShouldBeProblemAsync(HttpStatusCode.Conflict, "posted_immutable");
        (await factory.ScalarAsync<long>("SELECT count(*) FROM cost_transactions WHERE id = @id", ("id", posted.Id))).ShouldBe(1);
        (await factory.ScalarAsync<long>("SELECT count(*) FROM audit_events WHERE action = 'cost.draft_deleted' AND entity_id = @id",
            ("id", draft.Id.ToString()))).ShouldBe(1);
    }

    [Fact]
    public async Task Post_AsContributor_Returns403AndCostStaysDraft()
    {
        var (pm, projectId) = await ArrangeAsync();
        await pm.AddMemberAsync(projectId, await UserId(DemoUsers.Con1));
        var draft = await pm.CreateCostAsync(projectId, 100m);
        var con1 = await factory.CreateClientForAsync(DemoUsers.Con1);

        var response = await con1.PostAsJsonAsync($"/api/finance/costs/{draft.Id}/post", new PostCostCommand(draft.Version));

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await factory.ScalarAsync<string>("SELECT status FROM cost_transactions WHERE id = @id", ("id", draft.Id))).ShouldBe("draft");
    }

    [Fact]
    public async Task Post_ByProjectManagerOfAnotherProject_Returns404()
    {
        var (pm, projectId) = await ArrangeAsync();
        var draft = await pm.CreateCostAsync(projectId, 100m);
        var otherPm = await factory.CreateClientForAsync(DemoUsers.Pm3);

        var response = await otherPm.PostAsJsonAsync($"/api/finance/costs/{draft.Id}/post", new PostCostCommand(draft.Version));

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Post_UpdatesSpendRemainingAndUtilization_AndWritesAudit_AndSecondPostIs409()
    {
        var (pm, projectId) = await ArrangeAsync();
        var draft = await pm.CreateCostAsync(projectId, 25_000m);

        var posted = await pm.PostAsJsonAsync($"/api/finance/costs/{draft.Id}/post", new PostCostCommand(draft.Version));
        var again = await pm.PostAsJsonAsync($"/api/finance/costs/{draft.Id}/post", new PostCostCommand(draft.Version));

        posted.StatusCode.ShouldBe(HttpStatusCode.OK);
        var s = await pm.FinanceAsync(projectId);
        (s.ActualSpend, s.RemainingBudget, s.Utilization).ShouldBe((25_000m, 75_000m, 0.25m));
        (await factory.ScalarAsync<long>("SELECT count(*) FROM audit_events WHERE action = 'cost.posted' AND entity_id = @id",
            ("id", draft.Id.ToString()))).ShouldBe(1);
        (await factory.ScalarAsync<string>("SELECT posted_by_id::text FROM cost_transactions WHERE id = @id", ("id", draft.Id))).ShouldNotBeNull();
        await again.ShouldBeProblemAsync(HttpStatusCode.Conflict, "already_posted");
    }

    [Fact]
    public async Task Spend_ExcludesDraftsAndPostCutoffCosts_AndAReversalNetsOut()
    {
        var (pm, projectId) = await ArrangeAsync();
        await pm.CreateCostAsync(projectId, 1_000m);                                        // draft: never counts
        await pm.CreatePostedCostAsync(projectId, 2_000m, new DateOnly(2026, 8, 1));        // counts
        await pm.CreatePostedCostAsync(projectId, 4_000m, new DateOnly(2026, 10, 5));       // posted but after the cutoff
        var mistake = await pm.CreatePostedCostAsync(projectId, 500m, new DateOnly(2026, 8, 2));
        (await pm.PostAsJsonAsync($"/api/finance/costs/{mistake.Id}/reverse", new ReverseCostCommand("Duplicate invoice"))).EnsureSuccessStatusCode();

        var atCutoff = await pm.FinanceAsync(projectId);
        var later = await pm.FinanceAsync(projectId, "?asOf=2026-10-31");

        atCutoff.ActualSpend.ShouldBe(2_000m);
        later.ActualSpend.ShouldBe(6_000m);
    }

    [Fact]
    public async Task Post_WhenAuditInsertFails_RollsBackAndTheCostStaysDraft()
    {
        var (pm, projectId) = await ArrangeAsync();
        var draft = await pm.CreateCostAsync(projectId, 100m);
        await using var db = factory.CreateDbContext();
        await db.Database.ExecuteSqlRawAsync(
            $"""
            CREATE FUNCTION fn_test_fail_post_audit() RETURNS trigger AS $$ BEGIN
              IF NEW.action = 'cost.posted' AND NEW.project_id = {projectId} THEN RAISE EXCEPTION 'forced failure'; END IF;
              RETURN NEW; END $$ LANGUAGE plpgsql;
            CREATE TRIGGER trg_test_fail_post_audit BEFORE INSERT ON audit_events FOR EACH ROW EXECUTE FUNCTION fn_test_fail_post_audit();
            """);
        try
        {
            var response = await pm.PostAsJsonAsync($"/api/finance/costs/{draft.Id}/post", new PostCostCommand(draft.Version));

            await response.ShouldBeProblemAsync(HttpStatusCode.InternalServerError);
            (await factory.ScalarAsync<string>("SELECT status FROM cost_transactions WHERE id = @id", ("id", draft.Id))).ShouldBe("draft");
            (await factory.ScalarAsync<long>("SELECT count(*) FROM cost_transactions WHERE id = @id AND posted_at IS NOT NULL", ("id", draft.Id))).ShouldBe(0);
        }
        finally
        {
            await db.Database.ExecuteSqlRawAsync("DROP TRIGGER trg_test_fail_post_audit ON audit_events; DROP FUNCTION fn_test_fail_post_audit();");
        }
    }

    [Fact]
    public async Task Post_CrossingNinetyAndHundredPercent_QueuesOneOutboxMessageEach()
    {
        var (pm, projectId) = await ArrangeAsync();
        async Task<long> MessagesAsync() => await factory.ScalarAsync<long>(
            "SELECT count(*) FROM outbox_messages WHERE type = 'BudgetThresholdCrossed' AND dedup_key LIKE @k", ("k", $"budget:{projectId}:%"));

        await pm.CreatePostedCostAsync(projectId, 85_000m);
        (await MessagesAsync()).ShouldBe(0);
        await pm.CreatePostedCostAsync(projectId, 10_000m);   // 95%: crosses 90
        (await MessagesAsync()).ShouldBe(1);
        await pm.CreatePostedCostAsync(projectId, 1_000m);    // 96%: nothing new
        (await MessagesAsync()).ShouldBe(1);
        await pm.CreatePostedCostAsync(projectId, 10_000m);   // 106%: crosses 100
        (await MessagesAsync()).ShouldBe(2);
        await pm.CreatePostedCostAsync(projectId, 1_000m);    // still over: no repeat
        (await MessagesAsync()).ShouldBe(2);
        (await factory.ScalarAsync<string>("SELECT status FROM outbox_messages WHERE dedup_key = @k", ("k", $"budget:{projectId}:90"))).ShouldBe("pending");
    }

    [Fact]
    public async Task Reverse_CreatesLinkedNegativePostedRow_AndLeavesTheOriginalUntouched()
    {
        var (pm, projectId) = await ArrangeAsync();
        var original = await pm.CreatePostedCostAsync(projectId, 5_000m, new DateOnly(2026, 8, 10));

        var response = await pm.PostAsJsonAsync($"/api/finance/costs/{original.Id}/reverse", new ReverseCostCommand("Wrong project"));

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var reversal = (await response.Content.ReadFromJsonAsync<CostDto>())!;
        (reversal.Amount, reversal.Status, reversal.ReversesTransactionId, reversal.TxnDate).ShouldBe((-5_000m, "posted", original.Id, original.TxnDate));
        (await factory.ScalarAsync<decimal>("SELECT amount FROM cost_transactions WHERE id = @id", ("id", original.Id))).ShouldBe(5_000m);
        (await pm.FinanceAsync(projectId)).ActualSpend.ShouldBe(0m);
        (await factory.ScalarAsync<long>("SELECT count(*) FROM audit_events WHERE action = 'cost.reversed' AND entity_id = @id",
            ("id", reversal.Id.ToString()))).ShouldBe(1);
    }

    [Fact]
    public async Task Reverse_Twice_DraftOrReversal_AreConflicts_AndContributorIsForbidden()
    {
        var (pm, projectId) = await ArrangeAsync();
        await pm.AddMemberAsync(projectId, await UserId(DemoUsers.Con1));
        var posted = await pm.CreatePostedCostAsync(projectId, 100m);
        var draft = await pm.CreateCostAsync(projectId, 100m);
        var reason = new ReverseCostCommand("Mistake");
        var reversal = await (await pm.PostAsJsonAsync($"/api/finance/costs/{posted.Id}/reverse", reason)).Content.ReadFromJsonAsync<CostDto>();

        await (await pm.PostAsJsonAsync($"/api/finance/costs/{posted.Id}/reverse", reason)).ShouldBeProblemAsync(HttpStatusCode.Conflict, "already_reversed");
        await (await pm.PostAsJsonAsync($"/api/finance/costs/{draft.Id}/reverse", reason)).ShouldBeProblemAsync(HttpStatusCode.Conflict, "not_posted");
        await (await pm.PostAsJsonAsync($"/api/finance/costs/{reversal!.Id}/reverse", reason)).ShouldBeProblemAsync(HttpStatusCode.Conflict, "cannot_reverse_reversal");
        await (await pm.PostAsJsonAsync($"/api/finance/costs/{posted.Id}/reverse", new ReverseCostCommand(""))).ShouldBeProblemAsync(HttpStatusCode.BadRequest, "validation_failed");
        var con1 = await factory.CreateClientForAsync(DemoUsers.Con1);
        (await con1.PostAsJsonAsync($"/api/finance/costs/{posted.Id}/reverse", reason)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task PostedCost_CannotBeChangedByDirectSql_DatabaseTriggerBlocksIt()
    {
        var (pm, projectId) = await ArrangeAsync();
        var posted = await pm.CreatePostedCostAsync(projectId, 100m);
        await using var db = factory.CreateDbContext();

        var act = () => db.Database.ExecuteSqlRawAsync($"UPDATE cost_transactions SET amount = 1 WHERE id = {posted.Id}");

        (await Should.ThrowAsync<Npgsql.PostgresException>(act)).MessageText.ShouldContain("immutable");
    }
}
