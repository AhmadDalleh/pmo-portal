using System.Net;
using System.Net.Http.Json;
using Pmo.Application.Common;
using Pmo.Application.Features.Tasks.Dtos;
using Pmo.IntegrationTests.Infrastructure;
using Shouldly;

namespace Pmo.IntegrationTests.Tasks;

/// <summary>Stories C1 (list), C3 (my work), C4 (indicators) against the seed, as of 2026-09-30. Read-only.</summary>
[Collection(ApiCollection.Name)]
public class TaskReadTests(PmoApiFactory factory)
{
    private static async Task<PagedResult<TaskDto>> GetAsync(HttpClient client, string url) =>
        (await client.GetFromJsonAsync<PagedResult<TaskDto>>(url))!;

    [Fact]
    public async Task List_OverdueFilter_ReturnsOnlyOpenTasksDueBeforeAsOf()
    {
        var admin = await factory.CreateClientForAsync(DemoUsers.Admin1);

        var page = await GetAsync(admin, "/api/projects/1/tasks?overdue=true");

        page.Items.Count.ShouldBe(1);
        page.Items.ShouldAllBe(t => t.Indicator == "overdue" && t.DueDate < new DateOnly(2026, 9, 30) && t.Status != "done");
    }

    [Fact]
    public async Task List_TaskDueOnAsOfDate_IsDueTodayAndNotOverdue()
    {
        var admin = await factory.CreateClientForAsync(DemoUsers.Admin1);

        var all = await GetAsync(admin, "/api/projects/7/tasks?pageSize=100");
        var overdue = await GetAsync(admin, "/api/projects/7/tasks?overdue=true");

        all.Items.Single(t => t.Indicator == "due_today").DueDate.ShouldBe(new DateOnly(2026, 9, 30));
        overdue.TotalCount.ShouldBe(0);
    }

    [Fact]
    public async Task List_MovingAsOfForward_MakesTheDueTodayTaskOverdue()
    {
        var admin = await factory.CreateClientForAsync(DemoUsers.Admin1);

        var overdue = await GetAsync(admin, "/api/projects/7/tasks?overdue=true&asOf=2026-10-01");

        overdue.Items.Single().DueDate.ShouldBe(new DateOnly(2026, 9, 30));
    }

    [Fact]
    public async Task List_SortedByPriorityDescending_PutsCriticalFirst()
    {
        var admin = await factory.CreateClientForAsync(DemoUsers.Admin1);

        var page = await GetAsync(admin, "/api/projects/2/tasks?sort=priority&dir=desc&pageSize=100");

        page.Items.First().Priority.ShouldBe("critical");
        page.Items.Last().Priority.ShouldBe("low");
    }

    [Fact]
    public async Task List_UnknownSort_Returns400()
    {
        var admin = await factory.CreateClientForAsync(DemoUsers.Admin1);

        await (await admin.GetAsync("/api/projects/1/tasks?sort=password")).ShouldBeProblemAsync(HttpStatusCode.BadRequest, "validation_failed");
    }

    [Fact]
    public async Task List_ProjectOutsideScope_Returns404()
    {
        var con1 = await factory.CreateClientForAsync(DemoUsers.Con1);   // not a member of PRJ-002

        (await con1.GetAsync("/api/projects/2/tasks")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await con1.GetAsync("/api/tasks/" + await factory.ScalarAsync<int>("SELECT min(id) FROM tasks WHERE project_id = 2"))).StatusCode
            .ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task List_FilterByAssignee_ReturnsOnlyThatPersonsTasks()
    {
        var admin = await factory.CreateClientForAsync(DemoUsers.Admin1);
        var assignee = await factory.ScalarAsync<int>("SELECT assignee_id FROM tasks WHERE project_id = 1 AND assignee_id IS NOT NULL LIMIT 1");

        var page = await GetAsync(admin, $"/api/projects/1/tasks?assigneeId={assignee}&pageSize=100");

        page.Items.ShouldNotBeEmpty();
        page.Items.ShouldAllBe(t => t.AssigneeId == assignee);
    }

    [Fact]
    public async Task MyTasks_ReturnsOnlyMyOpenTasks_AndAllWhenAsked()
    {
        var con1 = await factory.CreateClientForAsync(DemoUsers.Con1);
        var me = await factory.ScalarAsync<int>("SELECT id FROM users WHERE email = 'con1@pmo.example'");

        var open = await GetAsync(con1, "/api/me/tasks?pageSize=100");
        var all = await GetAsync(con1, "/api/me/tasks?status=all&pageSize=100");

        open.Items.ShouldAllBe(t => t.AssigneeId == me && (t.Status == "todo" || t.Status == "in_progress" || t.Status == "blocked"));
        all.TotalCount.ShouldBeGreaterThan(open.TotalCount);
        open.Items.Select(t => t.DueDate).ShouldBe(open.Items.Select(t => t.DueDate).Order());   // soonest first
    }
}
