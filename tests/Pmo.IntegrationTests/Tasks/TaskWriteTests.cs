using System.Net;
using System.Net.Http.Json;
using Pmo.Application.Features.Projects.Dtos;
using Pmo.Application.Features.Tasks.Commands;
using Pmo.Application.Features.Tasks.Dtos;
using Pmo.IntegrationTests.Infrastructure;
using Shouldly;

namespace Pmo.IntegrationTests.Tasks;

/// <summary>Stories C1, C2, C6: create/edit tasks, contributor progress updates, live progress. Each test uses its own project.</summary>
[Collection(ApiCollection.Name)]
public class TaskWriteTests(PmoApiFactory factory)
{
    private Task<int> UserId(string email) => factory.ScalarAsync<int>("SELECT id FROM users WHERE email = @e", ("e", email));

    private static UpdateTaskProgressCommand Progress(TaskDto t, int pct, string status) => new(pct, status, t.Version);

    [Fact]
    public async Task Create_AsManager_PersistsTaskAndAudit()
    {
        var pm = await factory.CreateClientForAsync(DemoUsers.Pm5);
        var project = await pm.CreateProjectAsync();
        var con1 = await UserId(DemoUsers.Con1);
        await pm.AddMemberAsync(project.Id, con1);

        var task = await pm.CreateTaskAsync(project.Id, ApiTestHelpers.NewTask(assigneeId: con1, weight: 5));

        task.AssigneeName.ShouldNotBeNullOrEmpty();
        (await factory.ScalarAsync<short>("SELECT weight FROM tasks WHERE id = @id", ("id", task.Id))).ShouldBe((short)5);
        (await factory.ScalarAsync<long>("SELECT count(*) FROM audit_events WHERE action = 'task.created' AND entity_id = @id",
            ("id", task.Id.ToString()))).ShouldBe(1);
    }

    [Fact]
    public async Task Create_WithStatusDone_ForcesProgress100AndStampsCompletedAt()
    {
        var pm = await factory.CreateClientForAsync(DemoUsers.Pm5);
        var project = await pm.CreateProjectAsync();

        var task = await pm.CreateTaskAsync(project.Id, ApiTestHelpers.NewTask(status: "done", progress: 10));

        task.ProgressPct.ShouldBe(100);
        task.CompletedAt.ShouldNotBeNull();
    }

    [Fact]
    public async Task Create_AsContributor_Returns403()
    {
        var con1 = await factory.CreateClientForAsync(DemoUsers.Con1);

        var response = await con1.PostAsJsonAsync("/api/projects/1/tasks", ApiTestHelpers.NewTask());

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Create_AssigneeWhoIsNotAMember_Returns400()
    {
        var pm = await factory.CreateClientForAsync(DemoUsers.Pm5);
        var project = await pm.CreateProjectAsync();

        var response = await pm.PostAsJsonAsync($"/api/projects/{project.Id}/tasks", ApiTestHelpers.NewTask(assigneeId: await UserId(DemoUsers.Con1)));

        await response.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "assignee_not_member");
    }

    [Fact]
    public async Task Create_MilestoneFromAnotherProject_Returns400()
    {
        var pm = await factory.CreateClientForAsync(DemoUsers.Pm5);
        var mine = await pm.CreateProjectAsync();
        var other = await pm.CreateProjectAsync();
        var foreignMilestone = await pm.CreateMilestoneAsync(other.Id);

        var response = await pm.PostAsJsonAsync($"/api/projects/{mine.Id}/tasks", ApiTestHelpers.NewTask(milestoneId: foreignMilestone.Id));

        await response.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "milestone_not_in_project");
    }

    [Fact]
    public async Task Create_InvalidFields_Returns400WithFieldErrors()
    {
        var pm = await factory.CreateClientForAsync(DemoUsers.Pm5);
        var project = await pm.CreateProjectAsync();
        var bad = ApiTestHelpers.NewTask(title: "", weight: 11, progress: 101) with { StartDate = new DateOnly(2026, 12, 31) };

        var response = await pm.PostAsJsonAsync($"/api/projects/{project.Id}/tasks", bad);

        await response.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "validation_failed");
        (await response.FieldErrorsAsync("title")).ShouldNotBeEmpty();
        (await response.FieldErrorsAsync("weight")).ShouldNotBeEmpty();
        (await response.FieldErrorsAsync("progressPct")).ShouldNotBeEmpty();
        (await response.FieldErrorsAsync("dueDate")).ShouldNotBeEmpty();
    }

    [Fact]
    public async Task Update_AsManager_ChangesTaskAndRejectsStaleVersion()
    {
        var pm = await factory.CreateClientForAsync(DemoUsers.Pm5);
        var project = await pm.CreateProjectAsync();
        var task = await pm.CreateTaskAsync(project.Id, ApiTestHelpers.NewTask());
        UpdateTaskCommand Edit(string title) => new(title, null, null, null, "in_progress", "high", null, task.DueDate, 3, 20, task.Version);

        var first = await pm.PutAsJsonAsync($"/api/tasks/{task.Id}", Edit("Renamed"));
        var stale = await pm.PutAsJsonAsync($"/api/tasks/{task.Id}", Edit("Second tab"));

        first.StatusCode.ShouldBe(HttpStatusCode.OK);
        await stale.ShouldBeProblemAsync(HttpStatusCode.Conflict, "concurrency_conflict");
        (await factory.ScalarAsync<string>("SELECT title FROM tasks WHERE id = @id", ("id", task.Id))).ShouldBe("Renamed");
    }

    [Fact]
    public async Task Progress_ByAssignee_ChangesProjectProgressOnNextRead()
    {
        var pm = await factory.CreateClientForAsync(DemoUsers.Pm5);
        var project = await pm.CreateProjectAsync();
        var con1Id = await UserId(DemoUsers.Con1);
        await pm.AddMemberAsync(project.Id, con1Id);
        var light = await pm.CreateTaskAsync(project.Id, ApiTestHelpers.NewTask("light", con1Id, weight: 1));
        var heavy = await pm.CreateTaskAsync(project.Id, ApiTestHelpers.NewTask("heavy", con1Id, weight: 3));
        var con1 = await factory.CreateClientForAsync(DemoUsers.Con1);
        async Task<decimal?> ProgressAsync() =>
            (await pm.GetFromJsonAsync<ProjectSummaryDto>($"/api/projects/{project.Id}/summary"))!.Delivery.ProgressPct;

        (await ProgressAsync()).ShouldBe(0m);
        var done = await con1.PatchAsJsonAsync($"/api/tasks/{light.Id}/progress", Progress(light, 0, "done"));
        (await ProgressAsync()).ShouldBe(25m);   // (1*100 + 3*0) / 4
        await con1.PatchAsJsonAsync($"/api/tasks/{heavy.Id}/progress", Progress(heavy, 50, "in_progress"));

        done.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await ProgressAsync()).ShouldBe(62.5m);   // (1*100 + 3*50) / 4
    }

    [Fact]
    public async Task Progress_ByAnotherContributorOfTheSameProject_Returns403AndChangesNothing()
    {
        var pm = await factory.CreateClientForAsync(DemoUsers.Pm5);
        var project = await pm.CreateProjectAsync();
        var con1Id = await UserId(DemoUsers.Con1);
        await pm.AddMemberAsync(project.Id, con1Id);
        await pm.AddMemberAsync(project.Id, await UserId("con2@pmo.example"));
        var task = await pm.CreateTaskAsync(project.Id, ApiTestHelpers.NewTask(assigneeId: con1Id));
        var con2 = await factory.CreateClientForAsync("con2@pmo.example");

        var response = await con2.PatchAsJsonAsync($"/api/tasks/{task.Id}/progress", Progress(task, 90, "in_progress"));

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await factory.ScalarAsync<short>("SELECT progress_pct FROM tasks WHERE id = @id", ("id", task.Id))).ShouldBe((short)0);
    }

    [Fact]
    public async Task Progress_OnTaskOfAProjectOutsideScope_Returns404()
    {
        var pm = await factory.CreateClientForAsync(DemoUsers.Pm5);
        var project = await pm.CreateProjectAsync();
        var task = await pm.CreateTaskAsync(project.Id, ApiTestHelpers.NewTask());
        var con1 = await factory.CreateClientForAsync(DemoUsers.Con1);

        var response = await con1.PatchAsJsonAsync($"/api/tasks/{task.Id}/progress", Progress(task, 10, "in_progress"));

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Progress_ContributorCannotCancel_ButManagerCan()
    {
        var pm = await factory.CreateClientForAsync(DemoUsers.Pm5);
        var project = await pm.CreateProjectAsync();
        var con1Id = await UserId(DemoUsers.Con1);
        await pm.AddMemberAsync(project.Id, con1Id);
        var task = await pm.CreateTaskAsync(project.Id, ApiTestHelpers.NewTask(assigneeId: con1Id));
        var con1 = await factory.CreateClientForAsync(DemoUsers.Con1);

        (await con1.PatchAsJsonAsync($"/api/tasks/{task.Id}/progress", Progress(task, 0, "cancelled"))).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await pm.PatchAsJsonAsync($"/api/tasks/{task.Id}/progress", Progress(task, 0, "cancelled"))).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Progress_StaleVersion_Returns409_AndLeavingDoneClearsCompletedAt()
    {
        var pm = await factory.CreateClientForAsync(DemoUsers.Pm5);
        var project = await pm.CreateProjectAsync();
        var task = await pm.CreateTaskAsync(project.Id, ApiTestHelpers.NewTask());

        var done = await (await pm.PatchAsJsonAsync($"/api/tasks/{task.Id}/progress", Progress(task, 0, "done"))).Content.ReadFromJsonAsync<TaskDto>();
        var stale = await pm.PatchAsJsonAsync($"/api/tasks/{task.Id}/progress", Progress(task, 10, "in_progress"));
        var reopened = await (await pm.PatchAsJsonAsync($"/api/tasks/{task.Id}/progress", Progress(done!, 40, "in_progress"))).Content.ReadFromJsonAsync<TaskDto>();

        done!.CompletedAt.ShouldNotBeNull();
        await stale.ShouldBeProblemAsync(HttpStatusCode.Conflict, "concurrency_conflict");
        reopened!.CompletedAt.ShouldBeNull();
        reopened.ProgressPct.ShouldBe(40);
    }

    [Fact]
    public async Task Progress_OutOfRangeValue_Returns400()
    {
        var pm = await factory.CreateClientForAsync(DemoUsers.Pm5);
        var project = await pm.CreateProjectAsync();
        var task = await pm.CreateTaskAsync(project.Id, ApiTestHelpers.NewTask());

        var response = await pm.PatchAsJsonAsync($"/api/tasks/{task.Id}/progress", Progress(task, 101, "in_progress"));

        await response.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "validation_failed");
        (await response.FieldErrorsAsync("progressPct")).ShouldNotBeEmpty();
    }
}
