using System.Net;
using System.Net.Http.Json;
using Pmo.Application.Common;
using Pmo.Application.Features.Risks.Commands;
using Pmo.Application.Features.Risks.Dtos;
using Pmo.IntegrationTests.Infrastructure;
using Shouldly;

namespace Pmo.IntegrationTests.Risks;

/// <summary>Epic D. Writes use a project created by the test; the high-exposure checks read the seed.</summary>
[Collection(ApiCollection.Name)]
public class RiskTests(PmoApiFactory factory)
{
    private async Task<(HttpClient Pm, int ProjectId, int OwnerId)> ArrangeAsync()
    {
        var pm = await factory.CreateClientForAsync(DemoUsers.Pm5);
        var project = await pm.CreateProjectAsync();
        return (pm, project.Id, project.OwnerId);
    }

    private static CreateRiskCommand NewRisk(int ownerId, int probability = 2, int impact = 2, string? mitigation = null, string status = "open") =>
        new("risk", "Helper risk", "Something may go wrong", ownerId, probability, impact, mitigation, new DateOnly(2026, 12, 1), status);

    private static UpdateRiskCommand ToUpdate(RiskDto r, string? status = null, int? probability = null, string? mitigation = null) =>
        new(r.Type, r.Title, r.Description, r.OwnerId, probability ?? r.Probability, r.Impact, mitigation ?? r.Mitigation, r.DueDate, status ?? r.Status, r.Version);

    [Fact]
    public async Task Create_ComputesScoreInTheDatabase_AndHighExposureNeedsMitigation()
    {
        var (pm, projectId, ownerId) = await ArrangeAsync();

        var low = await pm.PostAsJsonAsync($"/api/projects/{projectId}/risks", NewRisk(ownerId, 2, 3));
        var highWithout = await pm.PostAsJsonAsync($"/api/projects/{projectId}/risks", NewRisk(ownerId, 4, 4));
        var highWith = await pm.PostAsJsonAsync($"/api/projects/{projectId}/risks", NewRisk(ownerId, 4, 4, "Add a second supplier"));

        low.StatusCode.ShouldBe(HttpStatusCode.Created);
        (await low.Content.ReadFromJsonAsync<RiskDto>())!.Score.ShouldBe(6);
        await highWithout.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "validation_failed");
        (await highWithout.FieldErrorsAsync("mitigation")).Single().ShouldContain("mitigation plan is required");
        var high = (await highWith.Content.ReadFromJsonAsync<RiskDto>())!;
        (high.Score, high.IsHighExposure).ShouldBe((16, true));
        (await factory.ScalarAsync<short>("SELECT score FROM risks WHERE id = @id", ("id", high.Id))).ShouldBe((short)16);
    }

    [Fact]
    public async Task Create_InvalidProbabilityOrOwnerOutsideTheProject_IsRejected()
    {
        var (pm, projectId, ownerId) = await ArrangeAsync();
        var outsider = await factory.ScalarAsync<int>("SELECT id FROM users WHERE email = 'con1@pmo.example'");

        var badProbability = await pm.PostAsJsonAsync($"/api/projects/{projectId}/risks", NewRisk(ownerId, 6, 1));
        var badOwner = await pm.PostAsJsonAsync($"/api/projects/{projectId}/risks", NewRisk(outsider));

        await badProbability.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "validation_failed");
        (await badProbability.FieldErrorsAsync("probability")).ShouldNotBeEmpty();
        await badOwner.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "owner_not_member");
    }

    [Fact]
    public async Task Create_AsContributorMember_Returns403()
    {
        var con1 = await factory.CreateClientForAsync(DemoUsers.Con1);

        var response = await con1.PostAsJsonAsync("/api/projects/1/risks", NewRisk(1));

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Closing_StampsClosedAt_AndTakesTheRiskOutOfTheOpenHighExposureView()
    {
        var (pm, projectId, ownerId) = await ArrangeAsync();
        var risk = (await (await pm.PostAsJsonAsync($"/api/projects/{projectId}/risks", NewRisk(ownerId, 5, 5, "Plan B"))).Content.ReadFromJsonAsync<RiskDto>())!;
        async Task<int> OpenHighAsync() =>
            (await pm.GetFromJsonAsync<PagedResult<RiskDto>>($"/api/risks?highExposure=true&projectId={projectId}"))!.TotalCount;
        (await OpenHighAsync()).ShouldBe(1);

        var closed = (await (await pm.PutAsJsonAsync($"/api/risks/{risk.Id}", ToUpdate(risk, "closed"))).Content.ReadFromJsonAsync<RiskDto>())!;
        var closedCount = await OpenHighAsync();
        var reopened = (await (await pm.PutAsJsonAsync($"/api/risks/{risk.Id}", ToUpdate(closed, "mitigating"))).Content.ReadFromJsonAsync<RiskDto>())!;

        closed.ClosedAt.ShouldNotBeNull();
        closedCount.ShouldBe(0);   // a closed high-score risk is not an open exposure
        reopened.ClosedAt.ShouldBeNull();
        (await OpenHighAsync()).ShouldBe(1);
    }

    [Fact]
    public async Task Update_WithStaleVersion_Returns409()
    {
        var (pm, projectId, ownerId) = await ArrangeAsync();
        var risk = (await (await pm.PostAsJsonAsync($"/api/projects/{projectId}/risks", NewRisk(ownerId))).Content.ReadFromJsonAsync<RiskDto>())!;
        (await pm.PutAsJsonAsync($"/api/risks/{risk.Id}", ToUpdate(risk, probability: 3))).EnsureSuccessStatusCode();

        var stale = await pm.PutAsJsonAsync($"/api/risks/{risk.Id}", ToUpdate(risk, probability: 4));

        await stale.ShouldBeProblemAsync(HttpStatusCode.Conflict, "concurrency_conflict");
    }

    [Fact]
    public async Task HighExposureView_OnSeed_ListsOnlyOpenItemsWithScore15OrMore_AndHidesTheClosedOne()
    {
        var admin = await factory.CreateClientForAsync(DemoUsers.Admin1);

        var open = (await admin.GetFromJsonAsync<PagedResult<RiskDto>>("/api/risks?highExposure=true&pageSize=100"))!;
        var closed = (await admin.GetFromJsonAsync<PagedResult<RiskDto>>("/api/risks?highExposure=true&status=closed"))!;

        open.Items.ShouldAllBe(r => r.Score >= 15 && r.Status != "closed");
        open.Items.Where(r => r.ProjectCode.StartsWith("PRJ-")).Count().ShouldBe(4);
        open.Items.Select(r => r.Score).ShouldBe(open.Items.Select(r => r.Score).OrderDescending());   // worst first
        closed.Items.ShouldContain(r => r.ProjectCode == "PRJ-006" && r.Score == 20);
    }

    [Fact]
    public async Task Search_RespectsScope_AndSupportsMatrixCells()
    {
        var con1 = await factory.CreateClientForAsync(DemoUsers.Con1);   // member of PRJ-001, 004, 007, 008
        var admin = await factory.CreateClientForAsync(DemoUsers.Admin1);

        var visible = (await con1.GetFromJsonAsync<PagedResult<RiskDto>>("/api/risks?status=all&pageSize=100"))!;
        var cell = (await admin.GetFromJsonAsync<PagedResult<RiskDto>>("/api/risks?status=all&probability=5&impact=5&pageSize=100"))!;

        visible.Items.Select(r => r.ProjectCode).Distinct().Order().ShouldBe(["PRJ-001", "PRJ-004", "PRJ-007", "PRJ-008"]);
        cell.Items.ShouldAllBe(r => r.Probability == 5 && r.Impact == 5);
        cell.Items.ShouldContain(r => r.ProjectCode == "PRJ-009");
    }

    [Fact]
    public async Task ProjectRiskList_OutsideScope_Returns404()
    {
        var con1 = await factory.CreateClientForAsync(DemoUsers.Con1);

        (await con1.GetAsync("/api/projects/2/risks")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }
}
