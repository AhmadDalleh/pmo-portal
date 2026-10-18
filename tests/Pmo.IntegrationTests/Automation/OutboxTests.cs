using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Pmo.Application.Common;
using Pmo.Application.Features.Admin;
using Pmo.Application.Features.Notifications;
using Pmo.Infrastructure.Outbox;
using Pmo.IntegrationTests.Infrastructure;
using Shouldly;

namespace Pmo.IntegrationTests.Automation;

/// <summary>Stories H1, H3, A6 and K4 #13. The background worker is switched off in tests; they run one pass explicitly.</summary>
[Collection(ApiCollection.Name)]
public class OutboxTests(PmoApiFactory factory)
{
    private async Task<OutboxRunResult> RunWorkerOnceAsync()
    {
        using var scope = factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<OutboxBatchRunner>().RunOnceAsync(CancellationToken.None);
    }

    private async Task<int> UserIdAsync(string email) => await factory.ScalarAsync<int>("SELECT id FROM users WHERE email = @e", ("e", email));

    /// <summary>Contributor con1 raises a change request in a new project of pm5; pm5 approves it. Returns the request id.</summary>
    private async Task<int> ApprovedChangeRequestAsync()
    {
        var pm = await factory.CreateClientForAsync(DemoUsers.Pm5);
        var project = await pm.CreateProjectAsync();
        await pm.AddMemberAsync(project.Id, await UserIdAsync(DemoUsers.Con1));
        var con = await factory.CreateClientForAsync(DemoUsers.Con1);
        var submitted = await con.SubmitChangeRequestAsync(await con.CreateChangeRequestAsync(project.Id));
        (await pm.DecideChangeRequestAsync(submitted, "approve")).EnsureSuccessStatusCode();
        return submitted.Id;
    }

    private Task<long> NotificationsForAsync(long outboxId) =>
        factory.ScalarAsync<long>("SELECT count(*) FROM notifications WHERE source_outbox_id = @id", ("id", outboxId));

    [Fact]
    public async Task Worker_TurnsAnApprovedChangeRequestIntoNotificationsForRequesterAndOwner_AndLogsARun()
    {
        var crId = await ApprovedChangeRequestAsync();
        var outboxId = await factory.ScalarAsync<long>("SELECT id FROM outbox_messages WHERE dedup_key = @k", ("k", $"cr-approved:{crId}"));

        var result = await RunWorkerOnceAsync();

        result.Failed.ShouldBe(0);
        result.Processed.ShouldBeGreaterThanOrEqualTo(1);
        (await factory.ScalarAsync<string>("SELECT status FROM outbox_messages WHERE id = @id", ("id", outboxId))).ShouldBe("processed");
        (await NotificationsForAsync(outboxId)).ShouldBe(2);   // con1 (requester) and pm5 (project owner)
        (await factory.ScalarAsync<long>("SELECT count(*) FROM automation_runs WHERE job_name = 'OutboxProcessor' AND status = 'succeeded' AND started_at > now() - interval '1 minute'"))
            .ShouldBeGreaterThanOrEqualTo(1);
        var con = await factory.CreateClientForAsync(DemoUsers.Con1);
        var mine = (await con.GetFromJsonAsync<PagedResult<NotificationDto>>("/api/notifications?unread=true"))!;
        mine.Items.ShouldContain(n => n.Title.Contains($"CR-{crId:D4}") && n.ReadAt == null);
    }

    [Fact]
    public async Task Worker_ProcessingTheSameMessageTwice_CreatesOneNotificationPerUser()
    {
        var crId = await ApprovedChangeRequestAsync();
        var outboxId = await factory.ScalarAsync<long>("SELECT id FROM outbox_messages WHERE dedup_key = @k", ("k", $"cr-approved:{crId}"));
        await RunWorkerOnceAsync();
        var before = await NotificationsForAsync(outboxId);

        await using var db = factory.CreateDbContext();   // simulate a duplicate delivery: put the message back
        await Microsoft.EntityFrameworkCore.RelationalDatabaseFacadeExtensions.ExecuteSqlAsync(
            db.Database, $"UPDATE outbox_messages SET status = 'pending', processed_at = NULL WHERE id = {outboxId}");
        await RunWorkerOnceAsync();

        (await factory.ScalarAsync<string>("SELECT status FROM outbox_messages WHERE id = @id", ("id", outboxId))).ShouldBe("processed");
        (await NotificationsForAsync(outboxId)).ShouldBe(before);   // UNIQUE (source_outbox_id, user_id) + ON CONFLICT DO NOTHING
    }

    [Fact]
    public async Task Worker_ForAMessageThatKeepsFailing_BacksOffThenMarksItFailed_AndAdminCanRetry()
    {
        var key = "test-bogus:" + Guid.NewGuid().ToString("N");
        await using var db = factory.CreateDbContext();
        await Microsoft.EntityFrameworkCore.RelationalDatabaseFacadeExtensions.ExecuteSqlAsync(db.Database,
            $"INSERT INTO outbox_messages (type, payload, dedup_key, status, occurred_at, available_at, attempts) VALUES ('Bogus', '{{}}', {key}, 'pending', now(), now() - interval '1 minute', 0)");   // clearly due: the database clock may run a few ms ahead of the host clock the worker uses
        var id = await factory.ScalarAsync<long>("SELECT id FROM outbox_messages WHERE dedup_key = @k", ("k", key));

        await RunWorkerOnceAsync();
        (await factory.ScalarAsync<int>("SELECT attempts FROM outbox_messages WHERE id = @id", ("id", id))).ShouldBe(1);
        (await factory.ScalarAsync<bool>("SELECT available_at > now() + interval '1 minute' FROM outbox_messages WHERE id = @id", ("id", id))).ShouldBeTrue();   // backoff
        (await factory.ScalarAsync<string>("SELECT last_error FROM outbox_messages WHERE id = @id", ("id", id))).ShouldContain("No handler");

        await RunWorkerOnceAsync();   // not due yet: untouched
        (await factory.ScalarAsync<int>("SELECT attempts FROM outbox_messages WHERE id = @id", ("id", id))).ShouldBe(1);

        for (var attempt = 2; attempt <= 5; attempt++)
        {
            await Microsoft.EntityFrameworkCore.RelationalDatabaseFacadeExtensions.ExecuteSqlAsync(db.Database, $"UPDATE outbox_messages SET available_at = now() - interval '1 minute' WHERE id = {id}");   // clearly due, same reason as above
            await RunWorkerOnceAsync();
        }

        (await factory.ScalarAsync<string>("SELECT status FROM outbox_messages WHERE id = @id", ("id", id))).ShouldBe("failed");
        (await factory.ScalarAsync<int>("SELECT attempts FROM outbox_messages WHERE id = @id", ("id", id))).ShouldBe(5);

        var admin = await factory.CreateClientForAsync(DemoUsers.Admin1);
        var retried = await admin.PostAsync($"/api/admin/automation/outbox/{id}/retry", null);
        retried.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await factory.ScalarAsync<string>("SELECT status FROM outbox_messages WHERE id = @id", ("id", id))).ShouldBe("pending");
        (await admin.PostAsync($"/api/admin/automation/outbox/{id}/retry", null)).StatusCode.ShouldBe(HttpStatusCode.Conflict);   // no longer failed
    }

    [Fact]
    public async Task Notifications_AreOwnedByTheirRecipient_AndCanBeMarkedRead()
    {
        var crId = await ApprovedChangeRequestAsync();
        await RunWorkerOnceAsync();
        var con = await factory.CreateClientForAsync(DemoUsers.Con1);
        var pm4 = await factory.CreateClientForAsync(DemoUsers.Pm4);
        var notification = (await con.GetFromJsonAsync<PagedResult<NotificationDto>>("/api/notifications?unread=true&pageSize=100"))!
            .Items.First(n => n.Title.Contains($"CR-{crId:D4}"));
        var before = await con.GetFromJsonAsync<int>("/api/notifications/unread-count");

        (await pm4.PostAsync($"/api/notifications/{notification.Id}/read", null)).StatusCode.ShouldBe(HttpStatusCode.NotFound);   // not theirs
        var read = await con.PostAsync($"/api/notifications/{notification.Id}/read", null);

        read.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await read.Content.ReadFromJsonAsync<NotificationDto>())!.ReadAt.ShouldNotBeNull();
        (await con.GetFromJsonAsync<int>("/api/notifications/unread-count")).ShouldBe(before - 1);
    }

    [Fact]
    public async Task AutomationMonitor_IsAdministratorOnly_AndFiltersByStatus()
    {
        var admin = await factory.CreateClientForAsync(DemoUsers.Admin1);
        var pm = await factory.CreateClientForAsync(DemoUsers.Pm1);

        var failed = (await admin.GetFromJsonAsync<PagedResult<OutboxMessageDto>>("/api/admin/automation/outbox?status=failed"))!;
        var runs = (await admin.GetFromJsonAsync<PagedResult<AutomationRunDto>>("/api/admin/automation/runs"))!;

        failed.Items.ShouldNotBeEmpty();
        failed.Items.ShouldAllBe(m => m.Status == "failed" && m.Attempts == 5);
        runs.TotalCount.ShouldBeGreaterThanOrEqualTo(20);
        (await pm.GetAsync("/api/admin/automation/runs")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await pm.PostAsync("/api/admin/automation/outbox/1/retry", null)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await admin.GetAsync("/api/admin/automation/outbox?status=weird")).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }
}
