using System.Net;
using System.Net.Http.Json;
using Pmo.Application.Features.Milestones.Commands;
using Pmo.Application.Features.Milestones.Dtos;
using Pmo.Application.Features.Snapshots.Dtos;
using Pmo.IntegrationTests.Infrastructure;
using Shouldly;

namespace Pmo.IntegrationTests.Tasks;

/// <summary>Stories C5 (milestones) and C7 (progress snapshots).</summary>
[Collection(ApiCollection.Name)]
public class MilestoneAndSnapshotTests(PmoApiFactory factory)
{
    [Fact]
    public async Task List_SeededOnHoldProject_ShowsOverdueMilestone()
    {
        var admin = await factory.CreateClientForAsync(DemoUsers.Admin1);

        var milestones = await admin.GetFromJsonAsync<List<MilestoneDto>>("/api/projects/6/milestones");

        milestones!.Where(m => m.IsOverdue).Select(m => m.DueDate).ShouldContain(new DateOnly(2026, 8, 31));
        milestones.Select(m => m.DueDate).ShouldBe(milestones.Select(m => m.DueDate).Order());
    }

    [Fact]
    public async Task Complete_SetsDateAndSecondCallReturns409()
    {
        var pm = await factory.CreateClientForAsync(DemoUsers.Pm5);
        var project = await pm.CreateProjectAsync();
        var milestone = await pm.CreateMilestoneAsync(project.Id);

        var first = await pm.PostAsJsonAsync($"/api/milestones/{milestone.Id}/complete", new CompleteMilestoneCommand(milestone.Version));
        var done = (await first.Content.ReadFromJsonAsync<MilestoneDto>())!;
        var second = await pm.PostAsJsonAsync($"/api/milestones/{milestone.Id}/complete", new CompleteMilestoneCommand(done.Version));

        done.Status.ShouldBe("completed");
        done.CompletedAt.ShouldNotBeNull();
        await second.ShouldBeProblemAsync(HttpStatusCode.Conflict, "already_completed");
    }

    [Fact]
    public async Task Update_StaleVersion_Returns409()
    {
        var pm = await factory.CreateClientForAsync(DemoUsers.Pm5);
        var project = await pm.CreateProjectAsync();
        var milestone = await pm.CreateMilestoneAsync(project.Id);
        var edit = new UpdateMilestoneCommand("Renamed", null, new DateOnly(2026, 12, 15), milestone.Version);

        (await pm.PutAsJsonAsync($"/api/milestones/{milestone.Id}", edit)).StatusCode.ShouldBe(HttpStatusCode.OK);
        var stale = await pm.PutAsJsonAsync($"/api/milestones/{milestone.Id}", edit with { Name = "Other tab" });

        await stale.ShouldBeProblemAsync(HttpStatusCode.Conflict, "concurrency_conflict");
    }

    [Fact]
    public async Task Create_AsContributor_Returns403_AndCountsShowTaskProgress()
    {
        var con1 = await factory.CreateClientForAsync(DemoUsers.Con1);
        var created = await con1.PostAsJsonAsync("/api/projects/1/milestones", new CreateMilestoneCommand("Nope", null, new DateOnly(2026, 12, 1)));
        created.StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        var pm = await factory.CreateClientForAsync(DemoUsers.Pm5);
        var project = await pm.CreateProjectAsync();
        var milestone = await pm.CreateMilestoneAsync(project.Id);
        await pm.CreateTaskAsync(project.Id, ApiTestHelpers.NewTask(milestoneId: milestone.Id, status: "done"));
        await pm.CreateTaskAsync(project.Id, ApiTestHelpers.NewTask(milestoneId: milestone.Id));

        var listed = (await pm.GetFromJsonAsync<List<MilestoneDto>>($"/api/projects/{project.Id}/milestones"))!.Single();

        (listed.TasksTotal, listed.TasksDone).ShouldBe((2, 1));
    }

    [Fact]
    public async Task History_ReturnsSeededMonthlySnapshotsInDateOrder()
    {
        var admin = await factory.CreateClientForAsync(DemoUsers.Admin1);

        var history = await admin.GetFromJsonAsync<List<ProgressSnapshotDto>>("/api/projects/1/progress-history");

        history!.Count.ShouldBeGreaterThanOrEqualTo(12);
        history.Select(h => h.SnapshotDate).ShouldBe(history.Select(h => h.SnapshotDate).Order());
        history.Single(h => h.SnapshotDate == new DateOnly(2026, 9, 30)).ActualSpend.ShouldBe(1_196_000m);   // later dates may exist: the run test adds one
    }

    [Fact]
    public async Task History_ProjectOutsideScope_Returns404()
    {
        var con1 = await factory.CreateClientForAsync(DemoUsers.Con1);

        (await con1.GetAsync("/api/projects/2/progress-history")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Run_AsAdministrator_IsIdempotentAndMatchesSql()
    {
        var admin = await factory.CreateClientForAsync(DemoUsers.Admin1);

        var first = await admin.PostAsync("/api/admin/snapshots/run?date=2026-10-31", null);
        var rowsAfterFirst = await factory.ScalarAsync<long>("SELECT count(*) FROM progress_snapshots WHERE snapshot_date = '2026-10-31'");
        var second = await admin.PostAsync("/api/admin/snapshots/run?date=2026-10-31", null);
        var rowsAfterSecond = await factory.ScalarAsync<long>("SELECT count(*) FROM progress_snapshots WHERE snapshot_date = '2026-10-31'");

        first.StatusCode.ShouldBe(HttpStatusCode.OK);
        second.StatusCode.ShouldBe(HttpStatusCode.OK);
        rowsAfterSecond.ShouldBe(rowsAfterFirst);   // upsert: no duplicates
        var expected = await factory.ScalarAsync<decimal>(
            "SELECT ROUND(SUM(weight * progress_pct)::numeric / SUM(weight), 2) FROM tasks WHERE project_id = 1 AND status <> 'cancelled'");
        (await factory.ScalarAsync<decimal>("SELECT progress_pct FROM progress_snapshots WHERE project_id = 1 AND snapshot_date = '2026-10-31'"))
            .ShouldBe(expected);
    }

    [Fact]
    public async Task Run_AsProjectManager_Returns403()
    {
        var pm = await factory.CreateClientForAsync(DemoUsers.Pm1);

        (await pm.PostAsync("/api/admin/snapshots/run", null)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }
}
