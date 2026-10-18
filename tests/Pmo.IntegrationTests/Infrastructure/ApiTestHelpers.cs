using System.Net.Http.Json;
using Pmo.Application.Features.Finance.Commands;
using Pmo.Application.Features.Finance.Dtos;
using Pmo.Application.Features.Milestones.Dtos;
using Pmo.Application.Features.Projects.Commands;
using Pmo.Application.Features.Projects.Dtos;
using Pmo.Application.Features.Tasks.Commands;
using Pmo.Application.Features.Tasks.Dtos;
using Shouldly;

namespace Pmo.IntegrationTests.Infrastructure;

/// <summary>Arrange helpers that go through the real API, so a test project looks exactly like one a user created.</summary>
public static class ApiTestHelpers
{
    private static int _counter;

    /// <summary>Creates a project owned by the signed-in project manager.</summary>
    public static async Task<ProjectDto> CreateProjectAsync(this HttpClient pm, decimal budget = 100_000m)
    {
        var code = $"T{Interlocked.Increment(ref _counter):D3}-{Random.Shared.Next(1000, 9999)}";
        var response = await pm.PostAsJsonAsync("/api/projects", new CreateProjectCommand(
            code, "Helper project", null, 1, null, new DateOnly(2026, 1, 1), new DateOnly(2027, 12, 31), budget));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ProjectDto>())!;
    }

    public static Task<HttpResponseMessage> PatchAsJsonAsync<T>(this HttpClient client, string url, T body) =>
        client.PatchAsync(url, JsonContent.Create(body));

    public static async Task AddMemberAsync(this HttpClient pm, int projectId, int userId, string role = "contributor")
        => (await pm.PostAsJsonAsync($"/api/projects/{projectId}/members", new AddMemberCommand(userId, role))).EnsureSuccessStatusCode();

    public static CreateTaskCommand NewTask(string title = "Helper task", int? assigneeId = null, int weight = 1, int progress = 0,
        string status = "todo", DateOnly? due = null, int? milestoneId = null) =>
        new(title, null, assigneeId, milestoneId, status, "medium", null, due ?? new DateOnly(2026, 12, 1), weight, progress);

    public static async Task<TaskDto> CreateTaskAsync(this HttpClient pm, int projectId, CreateTaskCommand command)
    {
        var response = await pm.PostAsJsonAsync($"/api/projects/{projectId}/tasks", command);
        response.StatusCode.ShouldBe(System.Net.HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<TaskDto>())!;
    }

    public static async Task<MilestoneDto> CreateMilestoneAsync(this HttpClient pm, int projectId, string name = "Helper milestone")
    {
        var response = await pm.PostAsJsonAsync($"/api/projects/{projectId}/milestones",
            new Pmo.Application.Features.Milestones.Commands.CreateMilestoneCommand(name, null, new DateOnly(2026, 11, 30)));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<MilestoneDto>())!;
    }

    /// <summary>Creates a draft cost (new Idempotency-Key unless one is given).</summary>
    public static async Task<HttpResponseMessage> PostCostAsync(this HttpClient pm, int projectId, decimal amount, DateOnly? date = null,
        int categoryId = 1, Guid? key = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, $"/api/projects/{projectId}/costs")
        {
            Content = JsonContent.Create(new CreateCostCommand(categoryId, date ?? new DateOnly(2026, 8, 1), amount, "Helper cost", "Test vendor", null))
        };
        request.Headers.Add("Idempotency-Key", (key ?? Guid.NewGuid()).ToString());
        return await pm.SendAsync(request);
    }

    public static async Task<CostDto> CreateCostAsync(this HttpClient pm, int projectId, decimal amount, DateOnly? date = null, int categoryId = 1)
    {
        var response = await pm.PostCostAsync(projectId, amount, date, categoryId);
        response.StatusCode.ShouldBe(System.Net.HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<CostDto>())!;
    }

    /// <summary>Creates a draft and posts it, the way the UI does (create, confirm dialog, post).</summary>
    public static async Task<CostDto> CreatePostedCostAsync(this HttpClient pm, int projectId, decimal amount, DateOnly? date = null)
    {
        var draft = await pm.CreateCostAsync(projectId, amount, date);
        var response = await pm.PostAsJsonAsync($"/api/finance/costs/{draft.Id}/post", new PostCostCommand(draft.Version));
        response.StatusCode.ShouldBe(System.Net.HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<CostDto>())!;
    }

    public static async Task<FinanceSummaryDto> FinanceAsync(this HttpClient client, int projectId, string asOf = "")
        => (await client.GetFromJsonAsync<FinanceSummaryDto>($"/api/projects/{projectId}/finance/summary{asOf}"))!;
}
