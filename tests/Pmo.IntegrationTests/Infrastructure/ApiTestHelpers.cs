using System.Net.Http.Json;
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
}
