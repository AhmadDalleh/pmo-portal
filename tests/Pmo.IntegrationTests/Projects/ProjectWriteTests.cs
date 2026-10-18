using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Pmo.Application.Features.Projects.Commands;
using Pmo.Application.Features.Projects.Dtos;
using Pmo.IntegrationTests.Infrastructure;
using Shouldly;

namespace Pmo.IntegrationTests.Projects;

/// <summary>Stories B1 and B2. Every test creates its own project, so the seeded numbers stay untouched.</summary>
[Collection(ApiCollection.Name)]
public class ProjectWriteTests(PmoApiFactory factory)
{
    private static int _counter;

    private static CreateProjectCommand NewProject(int? ownerId = null, decimal budget = 250_000m) => new(
        $"TST-{Interlocked.Increment(ref _counter):D3}-{Random.Shared.Next(1000, 9999)}", "Test project", "created by a test", 1, ownerId,
        new DateOnly(2026, 10, 1), new DateOnly(2027, 3, 31), budget);

    private static async Task<ProjectDto> CreateAsync(HttpClient client, CreateProjectCommand? command = null)
    {
        var response = await client.PostAsJsonAsync("/api/projects", command ?? NewProject());
        response.StatusCode.ShouldBe(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<ProjectDto>())!;
    }

    private static UpdateProjectCommand ToUpdate(ProjectDto p, string? status = null, DateOnly? actualEnd = null, int? ownerId = null) => new(
        p.Name + " v2", p.Description, ownerId ?? p.OwnerId, status ?? p.Status, p.StartDate, p.PlannedEndDate, actualEnd ?? p.ActualEndDate, p.Version);

    [Fact]
    public async Task Create_AsProjectManager_PersistsProjectBudgetOwnerMembershipAndAudit()
    {
        var pm = await factory.CreateClientForAsync(DemoUsers.Pm2);
        var command = NewProject();

        var response = await pm.PostAsJsonAsync("/api/projects", command);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var created = (await response.Content.ReadFromJsonAsync<ProjectDto>())!;
        response.Headers.Location!.ToString().ShouldEndWith($"/api/projects/{created.Id}");
        created.Status.ShouldBe("proposed");
        created.OwnerName.ShouldNotBeNullOrEmpty();

        // Assert in the database, not only in the response.
        (await factory.ScalarAsync<decimal>("SELECT initial_amount FROM project_budgets WHERE project_id = @id", ("id", created.Id))).ShouldBe(250_000m);
        (await factory.ScalarAsync<string>("SELECT member_role FROM project_members WHERE project_id = @id AND user_id = @owner",
            ("id", created.Id), ("owner", created.OwnerId))).ShouldBe("manager");
        (await factory.ScalarAsync<long>("SELECT count(*) FROM audit_events WHERE action = 'project.created' AND entity_id = @id",
            ("id", created.Id.ToString()))).ShouldBe(1);
    }

    [Fact]
    public async Task Create_AsContributor_Returns403AndNothingIsSaved()
    {
        var con = await factory.CreateClientForAsync(DemoUsers.Con1);
        var command = NewProject();

        var response = await con.PostAsJsonAsync("/api/projects", command);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await factory.ScalarAsync<long>("SELECT count(*) FROM projects WHERE code = @c", ("c", command.Code.ToUpperInvariant()))).ShouldBe(0);
    }

    [Fact]
    public async Task Create_PmNamingAnotherOwner_Returns403()
    {
        var pm1 = await factory.CreateClientForAsync(DemoUsers.Pm1);
        var pm2Id = await factory.ScalarAsync<int>("SELECT id FROM users WHERE email = 'pm2@pmo.example'");

        var response = await pm1.PostAsJsonAsync("/api/projects", NewProject(ownerId: pm2Id));

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Create_AsAdministrator_NeedsOwnerWhoIsAProjectManager()
    {
        var admin = await factory.CreateClientForAsync(DemoUsers.Admin1);
        var contributorId = await factory.ScalarAsync<int>("SELECT id FROM users WHERE email = 'con1@pmo.example'");

        await (await admin.PostAsJsonAsync("/api/projects", NewProject())).ShouldBeProblemAsync(HttpStatusCode.BadRequest, "owner_required");
        await (await admin.PostAsJsonAsync("/api/projects", NewProject(contributorId))).ShouldBeProblemAsync(HttpStatusCode.BadRequest, "owner_not_project_manager");
    }

    [Fact]
    public async Task Create_DuplicateCode_Returns409()
    {
        var pm = await factory.CreateClientForAsync(DemoUsers.Pm2);
        var first = NewProject();
        await CreateAsync(pm, first);

        var second = await pm.PostAsJsonAsync("/api/projects", first with { Code = first.Code.ToLowerInvariant() });

        await second.ShouldBeProblemAsync(HttpStatusCode.Conflict, "duplicate_project_code");
    }

    [Fact]
    public async Task Create_EndBeforeStartAndNegativeBudget_Returns400WithFieldErrors()
    {
        var pm = await factory.CreateClientForAsync(DemoUsers.Pm2);
        var bad = NewProject(budget: -1) with { PlannedEndDate = new DateOnly(2026, 9, 1) };

        var response = await pm.PostAsJsonAsync("/api/projects", bad);

        await response.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "validation_failed");
        (await response.FieldErrorsAsync("plannedEndDate")).Single().ShouldContain("on or after the start date");
        (await response.FieldErrorsAsync("initialBudget")).ShouldNotBeEmpty();
    }

    [Fact]
    public async Task Create_WhenBudgetInsertFails_RollsBackTheProjectToo()
    {
        // Force the second step of the transaction to fail with a trigger on project_budgets.
        await using var db = factory.CreateDbContext();
        await db.Database.ExecuteSqlRawAsync(
            """
            CREATE FUNCTION fn_test_fail_budget() RETURNS trigger AS $$ BEGIN RAISE EXCEPTION 'forced failure'; END $$ LANGUAGE plpgsql;
            CREATE TRIGGER trg_test_fail_budget BEFORE INSERT ON project_budgets FOR EACH ROW EXECUTE FUNCTION fn_test_fail_budget();
            """);
        var command = NewProject();
        try
        {
            var response = await (await factory.CreateClientForAsync(DemoUsers.Pm2)).PostAsJsonAsync("/api/projects", command);

            await response.ShouldBeProblemAsync(HttpStatusCode.InternalServerError, "internal_error");
            (await factory.ScalarAsync<long>("SELECT count(*) FROM projects WHERE code = @c", ("c", command.Code.ToUpperInvariant()))).ShouldBe(0);
            (await factory.ScalarAsync<long>("SELECT count(*) FROM audit_events WHERE action = 'project.created' AND data->>'Code' = @c",
                ("c", command.Code.ToUpperInvariant()))).ShouldBe(0);
        }
        finally
        {
            await db.Database.ExecuteSqlRawAsync("DROP TRIGGER trg_test_fail_budget ON project_budgets; DROP FUNCTION fn_test_fail_budget();");
        }
    }

    [Fact]
    public async Task Update_ByOwner_ChangesFieldsAndReturnsNewVersion()
    {
        var pm = await factory.CreateClientForAsync(DemoUsers.Pm2);
        var project = await CreateAsync(pm);

        var response = await pm.PutAsJsonAsync($"/api/projects/{project.Id}", ToUpdate(project, status: "active"));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var updated = (await response.Content.ReadFromJsonAsync<ProjectDto>())!;
        updated.Name.ShouldBe("Test project v2");
        updated.Status.ShouldBe("active");
        updated.Version.ShouldNotBe(project.Version);
        (await factory.ScalarAsync<string>("SELECT status FROM projects WHERE id = @id", ("id", project.Id))).ShouldBe("active");
    }

    [Fact]
    public async Task Update_WithStaleVersion_Returns409ConcurrencyConflict()
    {
        var pm = await factory.CreateClientForAsync(DemoUsers.Pm2);
        var project = await CreateAsync(pm);
        (await pm.PutAsJsonAsync($"/api/projects/{project.Id}", ToUpdate(project))).EnsureSuccessStatusCode();   // second tab saves first

        var stale = await pm.PutAsJsonAsync($"/api/projects/{project.Id}", ToUpdate(project) with { Name = "Overwrite attempt" });

        await stale.ShouldBeProblemAsync(HttpStatusCode.Conflict, "concurrency_conflict");
        (await factory.ScalarAsync<string>("SELECT name FROM projects WHERE id = @id", ("id", project.Id))).ShouldBe("Test project v2");
    }

    [Fact]
    public async Task Update_CompletedWithoutActualEndDate_Returns400()
    {
        var pm = await factory.CreateClientForAsync(DemoUsers.Pm2);
        var project = await CreateAsync(pm);

        var response = await pm.PutAsJsonAsync($"/api/projects/{project.Id}", ToUpdate(project, status: "completed"));

        await response.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "validation_failed");
        (await response.FieldErrorsAsync("actualEndDate")).Single().ShouldContain("actual end date");
    }

    [Fact]
    public async Task Update_ProjectManagerWhoOnlyContributes_Returns403_AndNonMemberGets404()
    {
        var pm4 = await factory.CreateClientForAsync(DemoUsers.Pm4);   // contributor member of PRJ-001, no access to PRJ-002
        var prj1 = (await pm4.GetFromJsonAsync<ProjectDto>("/api/projects/1"))!;
        var prj2 = (await (await factory.CreateClientForAsync(DemoUsers.Admin1)).GetFromJsonAsync<ProjectDto>("/api/projects/2"))!;

        (await pm4.PutAsJsonAsync("/api/projects/1", ToUpdate(prj1))).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await pm4.PutAsJsonAsync("/api/projects/2", ToUpdate(prj2))).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Update_AsContributor_Returns403()
    {
        var con = await factory.CreateClientForAsync(DemoUsers.Con1);
        var project = (await con.GetFromJsonAsync<ProjectDto>("/api/projects/1"))!;

        var response = await con.PutAsJsonAsync("/api/projects/1", ToUpdate(project));

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Update_ChangingOwner_MakesNewOwnerManagerAndDemotesTheOld()
    {
        var pm2 = await factory.CreateClientForAsync(DemoUsers.Pm2);
        var project = await CreateAsync(pm2);
        var pm3Id = await factory.ScalarAsync<int>("SELECT id FROM users WHERE email = 'pm3@pmo.example'");

        var response = await pm2.PutAsJsonAsync($"/api/projects/{project.Id}", ToUpdate(project, ownerId: pm3Id));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await factory.ScalarAsync<string>("SELECT member_role FROM project_members WHERE project_id = @id AND user_id = @u",
            ("id", project.Id), ("u", pm3Id))).ShouldBe("manager");
        (await factory.ScalarAsync<string>("SELECT member_role FROM project_members WHERE project_id = @id AND user_id = @u",
            ("id", project.Id), ("u", project.OwnerId))).ShouldBe("contributor");
    }
}
