using System.Net;
using System.Net.Http.Json;
using Pmo.Application.Features.Projects.Commands;
using Pmo.Application.Features.Projects.Dtos;
using Pmo.Domain.Delivery;
using Pmo.IntegrationTests.Infrastructure;
using Shouldly;

namespace Pmo.IntegrationTests.Projects;

/// <summary>Story B5. Each test creates its own project owned by pm5.</summary>
[Collection(ApiCollection.Name)]
public class ProjectMemberTests(PmoApiFactory factory)
{
    private static int _counter;

    private async Task<(HttpClient Pm, ProjectDto Project)> NewProjectAsync()
    {
        var pm = await factory.CreateClientForAsync(DemoUsers.Pm5);
        var code = $"MEM-{Interlocked.Increment(ref _counter):D2}-{Random.Shared.Next(1000, 9999)}";
        var response = await pm.PostAsJsonAsync("/api/projects", new CreateProjectCommand(
            code, "Member test", null, 3, null, new DateOnly(2026, 10, 1), new DateOnly(2027, 3, 31), 100_000m));
        return (pm, (await response.Content.ReadFromJsonAsync<ProjectDto>())!);
    }

    private Task<int> UserIdAsync(string email) => factory.ScalarAsync<int>("SELECT id FROM users WHERE email = @e", ("e", email));

    [Fact]
    public async Task Add_Contributor_AppearsInTheListAndGivesThemScope()
    {
        var (pm, project) = await NewProjectAsync();
        var con1 = await UserIdAsync(DemoUsers.Con1);

        var response = await pm.PostAsJsonAsync($"/api/projects/{project.Id}/members", new AddMemberCommand(con1, "contributor"));

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var members = await pm.GetFromJsonAsync<List<MemberDto>>($"/api/projects/{project.Id}/members");
        members!.Select(m => m.MemberRole).ShouldBe(["manager", "contributor"]);
        var contributor = await factory.CreateClientForAsync(DemoUsers.Con1);
        (await contributor.GetAsync($"/api/projects/{project.Id}")).StatusCode.ShouldBe(HttpStatusCode.OK);   // membership grants scope
    }

    [Fact]
    public async Task Add_SameUserTwice_Returns409()
    {
        var (pm, project) = await NewProjectAsync();
        var con1 = await UserIdAsync(DemoUsers.Con1);
        (await pm.PostAsJsonAsync($"/api/projects/{project.Id}/members", new AddMemberCommand(con1, "contributor"))).EnsureSuccessStatusCode();

        var second = await pm.PostAsJsonAsync($"/api/projects/{project.Id}/members", new AddMemberCommand(con1, "contributor"));

        await second.ShouldBeProblemAsync(HttpStatusCode.Conflict, "already_member");
    }

    [Fact]
    public async Task Add_ContributorAsManager_Returns400()
    {
        var (pm, project) = await NewProjectAsync();

        var response = await pm.PostAsJsonAsync($"/api/projects/{project.Id}/members", new AddMemberCommand(await UserIdAsync(DemoUsers.Con1), "manager"));

        await response.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "manager_must_be_pm");
    }

    [Fact]
    public async Task Add_AsContributorOfThatProject_Returns403()
    {
        var con1 = await factory.CreateClientForAsync(DemoUsers.Con1);   // member of PRJ-001, cannot manage it

        var response = await con1.PostAsJsonAsync("/api/projects/1/members", new AddMemberCommand(await UserIdAsync("con11@pmo.example"), "contributor"));

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Remove_TheOwner_Returns409()
    {
        var (pm, project) = await NewProjectAsync();

        var response = await pm.DeleteAsync($"/api/projects/{project.Id}/members/{project.OwnerId}");

        await response.ShouldBeProblemAsync(HttpStatusCode.Conflict, "cannot_remove_owner");
    }

    [Fact]
    public async Task Remove_MemberWithOpenTask_Returns409ListingTheTask_ThenSucceedsOnceDone()
    {
        var (pm, project) = await NewProjectAsync();
        var con1 = await UserIdAsync(DemoUsers.Con1);
        (await pm.PostAsJsonAsync($"/api/projects/{project.Id}/members", new AddMemberCommand(con1, "contributor"))).EnsureSuccessStatusCode();
        await using var db = factory.CreateDbContext();
        var task = new ProjectTask
        {
            ProjectId = project.Id, Title = "Open work item", AssigneeId = con1, Status = ProjectTaskStatus.InProgress,
            DueDate = new DateOnly(2026, 12, 1), CreatedAt = DateTimeOffset.UtcNow, CreatedById = project.OwnerId
        };
        db.Tasks.Add(task);
        await db.SaveChangesAsync();

        var blocked = await pm.DeleteAsync($"/api/projects/{project.Id}/members/{con1}");

        await blocked.ShouldBeProblemAsync(HttpStatusCode.Conflict, "member_has_open_tasks");
        (await blocked.ReadAsync()).GetProperty("detail").GetString().ShouldContain("Open work item");

        task.Status = ProjectTaskStatus.Done;
        task.ProgressPct = 100;
        await db.SaveChangesAsync();
        (await pm.DeleteAsync($"/api/projects/{project.Id}/members/{con1}")).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await factory.ScalarAsync<long>("SELECT count(*) FROM audit_events WHERE action = 'project.member_removed' AND project_id = @id",
            ("id", project.Id))).ShouldBe(1);
    }
}
