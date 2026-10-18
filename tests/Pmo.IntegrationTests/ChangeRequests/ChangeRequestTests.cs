using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Pmo.Application.Common;
using Pmo.Application.Features.ChangeRequests.Commands;
using Pmo.Application.Features.ChangeRequests.Dtos;
using Pmo.IntegrationTests.Infrastructure;
using Shouldly;

namespace Pmo.IntegrationTests.ChangeRequests;

/// <summary>Epic E and acceptance tests C and D. Each test builds its own project (owned by pm5) with con1 as a contributor member.</summary>
[Collection(ApiCollection.Name)]
public class ChangeRequestTests(PmoApiFactory factory)
{
    private async Task<(HttpClient Pm, HttpClient Con, int ProjectId)> ArrangeAsync()
    {
        var pm = await factory.CreateClientForAsync(DemoUsers.Pm5);
        var project = await pm.CreateProjectAsync();
        await pm.AddMemberAsync(project.Id, await factory.ScalarAsync<int>("SELECT id FROM users WHERE email = 'con1@pmo.example'"));
        return (pm, await factory.CreateClientForAsync(DemoUsers.Con1), project.Id);
    }

    private Task<long> HistoryRowsAsync(int crId) =>
        factory.ScalarAsync<long>("SELECT count(*) FROM change_request_history WHERE change_request_id = @id", ("id", crId));

    private Task<string> StatusAsync(int crId) =>
        factory.ScalarAsync<string>("SELECT status FROM change_requests WHERE id = @id", ("id", crId));

    [Fact]
    public async Task Create_AsProjectMember_StartsAsDraftWithCodeHistoryAndAudit()
    {
        var (_, con, projectId) = await ArrangeAsync();

        var cr = await con.CreateChangeRequestAsync(projectId);

        cr.Status.ShouldBe("draft");
        cr.Code.ShouldBe($"CR-{cr.Id:D4}");
        (await HistoryRowsAsync(cr.Id)).ShouldBe(1);
        (await factory.ScalarAsync<string?>("SELECT from_status FROM change_request_history WHERE change_request_id = @id", ("id", cr.Id))).ShouldBeNull();
        (await factory.ScalarAsync<long>("SELECT count(*) FROM audit_events WHERE action = 'change_request.created' AND entity_id = @id", ("id", cr.Id.ToString()))).ShouldBe(1);
    }

    [Fact]
    public async Task Create_InAProjectOutsideScope_Returns404()
    {
        var con1 = await factory.CreateClientForAsync(DemoUsers.Con1);   // not a member of PRJ-002

        var response = await con1.PostAsJsonAsync("/api/projects/2/change-requests", new CreateChangeRequestCommand("x", "y", "scope", null, null));

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Create_InvalidFields_Returns400WithFieldErrors()
    {
        var (_, con, projectId) = await ArrangeAsync();

        var response = await con.PostAsJsonAsync($"/api/projects/{projectId}/change-requests", new CreateChangeRequestCommand("", "", "wish", null, null));

        await response.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "validation_failed");
        (await response.FieldErrorsAsync("title")).ShouldNotBeEmpty();
        (await response.FieldErrorsAsync("type")).ShouldNotBeEmpty();
    }

    [Fact]
    public async Task Approve_WritesExactlyOneHistoryRow_AuditAndOutboxMessage_AndDoesNotTouchTheBudget()
    {
        var (pm, con, projectId) = await ArrangeAsync();
        var budgetBefore = (await pm.FinanceAsync(projectId)).CurrentBudget;
        var submitted = await con.SubmitChangeRequestAsync(await con.CreateChangeRequestAsync(projectId, "budget", 50_000m));
        var rowsBefore = await HistoryRowsAsync(submitted.Id);

        var response = await pm.DecideChangeRequestAsync(submitted, "approve", "Agreed");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var approved = (await response.Content.ReadFromJsonAsync<ChangeRequestDto>())!;
        (approved.Status, approved.DecisionReason, approved.DecidedByName).ShouldBe(("approved", "Agreed", (await pm.GetFromJsonAsync<Pmo.Application.Features.Auth.MeDto>("/api/auth/me"))!.FullName));
        (await HistoryRowsAsync(submitted.Id)).ShouldBe(rowsBefore + 1);   // exactly one new row
        (await factory.ScalarAsync<string>("SELECT to_status FROM change_request_history WHERE change_request_id = @id ORDER BY id DESC LIMIT 1", ("id", submitted.Id))).ShouldBe("approved");
        (await factory.ScalarAsync<string>("SELECT status FROM outbox_messages WHERE dedup_key = @k", ("k", $"cr-approved:{submitted.Id}"))).ShouldBe("pending");
        (await factory.ScalarAsync<long>("SELECT count(*) FROM audit_events WHERE action = 'change_request.decided' AND entity_id = @id", ("id", submitted.Id.ToString()))).ShouldBe(1);
        (await pm.FinanceAsync(projectId)).CurrentBudget.ShouldBe(budgetBefore);   // E5: approval never changes the budget by itself
    }

    [Fact]
    public async Task Decide_AsContributor_Returns403_AndNothingChanges()
    {
        var (_, con, projectId) = await ArrangeAsync();
        var submitted = await con.SubmitChangeRequestAsync(await con.CreateChangeRequestAsync(projectId));
        var rows = await HistoryRowsAsync(submitted.Id);
        var other = await factory.CreateClientForAsync("con2@pmo.example");

        var response = await other.DecideChangeRequestAsync(submitted, "approve");

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await StatusAsync(submitted.Id)).ShouldBe("submitted");
        (await HistoryRowsAsync(submitted.Id)).ShouldBe(rows);
    }

    [Fact]
    public async Task Decide_OwnRequest_Returns403_AndTheDatabaseCheckRejectsItToo()
    {
        var pm = await factory.CreateClientForAsync(DemoUsers.Pm5);
        var project = await pm.CreateProjectAsync();
        var own = await pm.SubmitChangeRequestAsync(await pm.CreateChangeRequestAsync(project.Id));

        var response = await pm.DecideChangeRequestAsync(own, "approve");

        await response.ShouldBeProblemAsync(HttpStatusCode.Forbidden, "self_approval_forbidden");
        (await StatusAsync(own.Id)).ShouldBe("submitted");
        await using var db = factory.CreateDbContext();
        var act = () => db.Database.ExecuteSqlAsync(
            $"UPDATE change_requests SET status = 'approved', decided_by_id = requested_by_id, decided_at = now(), decision_reason = 'self' WHERE id = {own.Id}");
        (await Should.ThrowAsync<PostgresException>(act)).SqlState.ShouldBe("23514");   // check_violation
    }

    [Fact]
    public async Task Decide_ByProjectManagerWhoCannotManageThatProject_Returns403Or404()
    {
        var submittedInPrj1 = await factory.ScalarAsync<int>("SELECT id FROM change_requests WHERE project_id = 1 AND status = 'submitted' LIMIT 1");
        var version = await factory.ScalarAsync<long>("SELECT xmin::text::bigint FROM change_requests WHERE id = @id", ("id", submittedInPrj1));
        var body = new DecideChangeRequestCommand("approve", "x", (uint)version);
        var pm4 = await factory.CreateClientForAsync(DemoUsers.Pm4);   // contributor member of PRJ-001: in scope, not a manager
        var pm3 = await factory.CreateClientForAsync(DemoUsers.Pm3);   // no access at all

        (await pm4.PostAsJsonAsync($"/api/approvals/change-requests/{submittedInPrj1}/decision", body)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await pm3.PostAsJsonAsync($"/api/approvals/change-requests/{submittedInPrj1}/decision", body)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await StatusAsync(submittedInPrj1)).ShouldBe("submitted");
    }

    [Fact]
    public async Task InvalidTransitions_Return409()
    {
        var (pm, con, projectId) = await ArrangeAsync();
        var draft = await con.CreateChangeRequestAsync(projectId);
        var approved = (await (await pm.DecideChangeRequestAsync(await con.SubmitChangeRequestAsync(await con.CreateChangeRequestAsync(projectId)), "approve"))
            .Content.ReadFromJsonAsync<ChangeRequestDto>())!;

        await (await pm.DecideChangeRequestAsync(draft, "approve")).ShouldBeProblemAsync(HttpStatusCode.Conflict, "invalid_transition");            // draft -> approved
        await (await con.PostAsJsonAsync($"/api/change-requests/{approved.Id}/submit", new SubmitChangeRequestCommand(approved.Version)))
            .ShouldBeProblemAsync(HttpStatusCode.Conflict, "invalid_transition");                                                                        // approved -> submitted
        await (await con.PostAsJsonAsync($"/api/change-requests/{approved.Id}/withdraw", new WithdrawChangeRequestCommand(null, approved.Version)))
            .ShouldBeProblemAsync(HttpStatusCode.Conflict, "invalid_transition");                                                                        // approved is terminal
        await (await pm.DecideChangeRequestAsync(approved, "reject")).ShouldBeProblemAsync(HttpStatusCode.Conflict, "invalid_transition");           // already decided
    }

    [Fact]
    public async Task Decide_WithStaleVersion_Returns409_AndMissingReasonReturns400()
    {
        var (pm, con, projectId) = await ArrangeAsync();
        var submitted = await con.SubmitChangeRequestAsync(await con.CreateChangeRequestAsync(projectId));

        var noReason = await pm.PostAsJsonAsync($"/api/approvals/change-requests/{submitted.Id}/decision", new DecideChangeRequestCommand("approve", "", submitted.Version));
        var stale = await pm.PostAsJsonAsync($"/api/approvals/change-requests/{submitted.Id}/decision", new DecideChangeRequestCommand("approve", "ok", submitted.Version - 1));

        await noReason.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "validation_failed");
        (await noReason.FieldErrorsAsync("reason")).ShouldNotBeEmpty();
        await stale.ShouldBeProblemAsync(HttpStatusCode.Conflict, "concurrency_conflict");
        (await StatusAsync(submitted.Id)).ShouldBe("submitted");
    }

    [Fact]
    public async Task Decide_WhenTheOutboxInsertFails_RollsBackStatusHistoryAndAudit()
    {
        var (pm, con, projectId) = await ArrangeAsync();
        var submitted = await con.SubmitChangeRequestAsync(await con.CreateChangeRequestAsync(projectId));
        var rows = await HistoryRowsAsync(submitted.Id);
        await using var db = factory.CreateDbContext();
        await db.Database.ExecuteSqlRawAsync(
            """
            CREATE FUNCTION fn_test_fail_outbox() RETURNS trigger AS $$ BEGIN RAISE EXCEPTION 'forced failure'; END $$ LANGUAGE plpgsql;
            CREATE TRIGGER trg_test_fail_outbox BEFORE INSERT ON outbox_messages FOR EACH ROW EXECUTE FUNCTION fn_test_fail_outbox();
            """);
        try
        {
            var response = await pm.DecideChangeRequestAsync(submitted, "approve");

            await response.ShouldBeProblemAsync(HttpStatusCode.InternalServerError);
            (await StatusAsync(submitted.Id)).ShouldBe("submitted");
            (await HistoryRowsAsync(submitted.Id)).ShouldBe(rows);   // no orphan history row
            (await factory.ScalarAsync<long>("SELECT count(*) FROM audit_events WHERE action = 'change_request.decided' AND entity_id = @id", ("id", submitted.Id.ToString()))).ShouldBe(0);
        }
        finally
        {
            await db.Database.ExecuteSqlRawAsync("DROP TRIGGER trg_test_fail_outbox ON outbox_messages; DROP FUNCTION fn_test_fail_outbox();");
        }
    }

    [Fact]
    public async Task Return_EditResubmitApprove_KeepsTheWholeTimeline()
    {
        var (pm, con, projectId) = await ArrangeAsync();
        var submitted = await con.SubmitChangeRequestAsync(await con.CreateChangeRequestAsync(projectId));
        var returned = (await (await pm.DecideChangeRequestAsync(submitted, "return", "Add an estimate")).Content.ReadFromJsonAsync<ChangeRequestDto>())!;

        var edited = await con.PutAsJsonAsync($"/api/change-requests/{returned.Id}",
            new UpdateChangeRequestCommand("Better title", "With estimate", "scope", 1_000m, 3, returned.Version));
        var resubmitted = await con.SubmitChangeRequestAsync((await edited.Content.ReadFromJsonAsync<ChangeRequestDto>())!);
        await pm.DecideChangeRequestAsync(resubmitted, "approve", "Good now");

        edited.StatusCode.ShouldBe(HttpStatusCode.OK);
        (resubmitted.DecisionReason, resubmitted.DecidedByName).ShouldBe((null, null));   // a resubmission clears the live decision fields
        var history = (await pm.GetFromJsonAsync<List<ChangeRequestHistoryDto>>($"/api/change-requests/{returned.Id}/history"))!;
        history.Select(h => h.ToStatus).ShouldBe(["draft", "submitted", "returned", "submitted", "approved"]);
        history.Select(h => h.FromStatus).ShouldBe([null, "draft", "submitted", "returned", "submitted"]);
        history[2].Reason.ShouldBe("Add an estimate");
        history.ShouldAllBe(h => h.ActorName != "");
    }

    [Fact]
    public async Task Update_ByAnotherMemberOrAfterSubmission_IsRefused()
    {
        var (pm, con, projectId) = await ArrangeAsync();
        var draft = await con.CreateChangeRequestAsync(projectId);
        UpdateChangeRequestCommand Edit(uint version) => new("T", "D", "scope", null, null, version);

        var byOther = await pm.PutAsJsonAsync($"/api/change-requests/{draft.Id}", Edit(draft.Version));
        var submitted = await con.SubmitChangeRequestAsync(draft);
        var afterSubmit = await con.PutAsJsonAsync($"/api/change-requests/{draft.Id}", Edit(submitted.Version));

        await byOther.ShouldBeProblemAsync(HttpStatusCode.Forbidden, "only_requester");
        await afterSubmit.ShouldBeProblemAsync(HttpStatusCode.Conflict, "not_editable");
    }

    [Fact]
    public async Task Withdraw_Draft_IsTerminal()
    {
        var (_, con, projectId) = await ArrangeAsync();
        var draft = await con.CreateChangeRequestAsync(projectId);

        var response = await con.PostAsJsonAsync($"/api/change-requests/{draft.Id}/withdraw", new WithdrawChangeRequestCommand("Not needed", draft.Version));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await StatusAsync(draft.Id)).ShouldBe("withdrawn");
        (await HistoryRowsAsync(draft.Id)).ShouldBe(2);
    }

    [Fact]
    public async Task ReviewQueue_ShowsSubmittedRequestsOfManagedProjects_ExcludingOwnAndOthers()
    {
        var (pm, con, projectId) = await ArrangeAsync();
        var theirs = await con.SubmitChangeRequestAsync(await con.CreateChangeRequestAsync(projectId));
        var own = await pm.SubmitChangeRequestAsync(await pm.CreateChangeRequestAsync(projectId));
        await con.CreateChangeRequestAsync(projectId);   // a draft: not in the queue

        var queue = (await pm.GetFromJsonAsync<PagedResult<ChangeRequestDto>>($"/api/approvals/change-requests?projectId={projectId}"))!;
        var otherPm = (await (await factory.CreateClientForAsync(DemoUsers.Pm3)).GetFromJsonAsync<PagedResult<ChangeRequestDto>>($"/api/approvals/change-requests?projectId={projectId}"))!;
        var admin = (await (await factory.CreateClientForAsync(DemoUsers.Admin1)).GetFromJsonAsync<PagedResult<ChangeRequestDto>>($"/api/approvals/change-requests?projectId={projectId}"))!;

        queue.Items.Select(c => c.Id).ShouldBe([theirs.Id]);
        otherPm.TotalCount.ShouldBe(0);
        admin.Items.Select(c => c.Id).ShouldBe([theirs.Id, own.Id]);   // the administrator may decide the PM's own request
        (await con.GetAsync("/api/approvals/change-requests")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task History_OfARequestOutsideScope_Returns404()
    {
        var con1 = await factory.CreateClientForAsync(DemoUsers.Con1);
        var foreign = await factory.ScalarAsync<int>("SELECT id FROM change_requests WHERE project_id = 2 LIMIT 1");

        (await con1.GetAsync($"/api/change-requests/{foreign}/history")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task SeededHistory_ShowsTheFullCycleOfTheReturnedAndApprovedRequest()
    {
        var admin = await factory.CreateClientForAsync(DemoUsers.Admin1);

        var history = (await admin.GetFromJsonAsync<List<ChangeRequestHistoryDto>>("/api/change-requests/2/history"))!;

        history.Select(h => h.ToStatus).ShouldBe(["draft", "submitted", "returned", "submitted", "approved"]);
    }
}
