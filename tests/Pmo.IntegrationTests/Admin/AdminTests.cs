using System.Net;
using System.Net.Http.Json;
using Pmo.Application.Common;
using Pmo.Application.Common.Abstractions;
using Pmo.Application.Features.Admin;
using Pmo.IntegrationTests.Infrastructure;
using Shouldly;

namespace Pmo.IntegrationTests.Admin;

/// <summary>Stories A4 and A5. con8 and con7 are reserved for these tests and restored at the end.</summary>
[Collection(ApiCollection.Name)]
public class AdminTests(PmoApiFactory factory)
{
    private Task<int> UserIdAsync(string email) => factory.ScalarAsync<int>("SELECT id FROM users WHERE email = @e", ("e", email));

    [Fact]
    public async Task Users_AreVisibleToAdministratorsOnly()
    {
        var admin = await factory.CreateClientForAsync(DemoUsers.Admin1);
        var pm = await factory.CreateClientForAsync(DemoUsers.Pm1);

        var users = await admin.GetFromJsonAsync<List<UserSummary>>("/api/admin/users");

        users!.Count.ShouldBeGreaterThanOrEqualTo(18);
        (await pm.GetAsync("/api/admin/users")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await pm.GetAsync("/api/admin/audit-events")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task ChangeRole_PromotesAndDemotes_WritesAuditWithBeforeAndAfter_AndDowngradesManagerMemberships()
    {
        var admin = await factory.CreateClientForAsync(DemoUsers.Admin1);
        var id = await UserIdAsync("con8@pmo.example");

        var promoted = await admin.PutAsJsonAsync($"/api/admin/users/{id}/role", new ChangeRoleCommand("ProjectManager"));
        promoted.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await promoted.Content.ReadFromJsonAsync<UserSummary>())!.Role.ShouldBe("ProjectManager");
        await (await factory.CreateClientForAsync("con8@pmo.example")).CreateProjectAsync();   // can now create projects... and owns one

        var blocked = await admin.PutAsJsonAsync($"/api/admin/users/{id}/role", new ChangeRoleCommand("Contributor"));
        await blocked.ShouldBeProblemAsync(HttpStatusCode.Conflict, "user_owns_projects");

        await using var db = factory.CreateDbContext();
        await Microsoft.EntityFrameworkCore.RelationalDatabaseFacadeExtensions.ExecuteSqlAsync(db.Database,
            $"UPDATE projects SET owner_id = (SELECT id FROM users WHERE email = 'pm5@pmo.example') WHERE owner_id = {id}");
        var demoted = await admin.PutAsJsonAsync($"/api/admin/users/{id}/role", new ChangeRoleCommand("Contributor"));
        demoted.StatusCode.ShouldBe(HttpStatusCode.OK);

        (await factory.ScalarAsync<long>("SELECT count(*) FROM project_members WHERE user_id = @u AND member_role = 'manager'", ("u", id))).ShouldBe(0);
        (await factory.ScalarAsync<string>("SELECT data::text FROM audit_events WHERE action = 'user.role_changed' AND entity_id = @id ORDER BY id DESC LIMIT 1",
            ("id", id.ToString()))).ShouldContain("ProjectManager");
    }

    [Fact]
    public async Task ChangeRole_OwnRoleInvalidRoleAndUnknownUser_AreRejected()
    {
        var admin = await factory.CreateClientForAsync(DemoUsers.Admin1);
        var me = await UserIdAsync(DemoUsers.Admin1);

        await (await admin.PutAsJsonAsync($"/api/admin/users/{me}/role", new ChangeRoleCommand("Contributor"))).ShouldBeProblemAsync(HttpStatusCode.Conflict, "cannot_change_own_role");
        await (await admin.PutAsJsonAsync($"/api/admin/users/{await UserIdAsync("con7@pmo.example")}/role", new ChangeRoleCommand("Boss"))).ShouldBeProblemAsync(HttpStatusCode.BadRequest, "invalid_role");
        (await admin.PutAsJsonAsync("/api/admin/users/99999/role", new ChangeRoleCommand("Contributor"))).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        await (await admin.PutAsJsonAsync($"/api/admin/users/{await UserIdAsync(DemoUsers.Pm1)}/role", new ChangeRoleCommand("Contributor"))).ShouldBeProblemAsync(HttpStatusCode.Conflict, "user_owns_projects");
    }

    [Fact]
    public async Task Deactivate_BlocksSignInAndRevokesRefreshTokens_ButNotForYourself()
    {
        var admin = await factory.CreateClientForAsync(DemoUsers.Admin1);
        var id = await UserIdAsync("con7@pmo.example");
        var client = factory.CreateClient();
        (await client.PostAsJsonAsync("/api/auth/login", new Pmo.Application.Features.Auth.LoginCommand("con7@pmo.example", PmoApiFactory.DemoPassword))).EnsureSuccessStatusCode();
        (await factory.ScalarAsync<long>("SELECT count(*) FROM user_tokens WHERE user_id = @u", ("u", id))).ShouldBeGreaterThan(0);

        (await admin.PutAsJsonAsync($"/api/admin/users/{id}/status", new SetUserStatusCommand(false))).EnsureSuccessStatusCode();
        try
        {
            (await client.PostAsJsonAsync("/api/auth/login", new Pmo.Application.Features.Auth.LoginCommand("con7@pmo.example", PmoApiFactory.DemoPassword)))
                .StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
            (await factory.ScalarAsync<long>("SELECT count(*) FROM user_tokens WHERE user_id = @u", ("u", id))).ShouldBe(0);
            await (await admin.PutAsJsonAsync($"/api/admin/users/{await UserIdAsync(DemoUsers.Admin1)}/status", new SetUserStatusCommand(false)))
                .ShouldBeProblemAsync(HttpStatusCode.Conflict, "cannot_deactivate_self");
        }
        finally
        {
            (await admin.PutAsJsonAsync($"/api/admin/users/{id}/status", new SetUserStatusCommand(true))).EnsureSuccessStatusCode();
        }
    }

    [Fact]
    public async Task AuditLog_FiltersByActionAndProject_NewestFirst()
    {
        var admin = await factory.CreateClientForAsync(DemoUsers.Admin1);

        var page = (await admin.GetFromJsonAsync<PagedResult<AuditEventDto>>("/api/admin/audit-events?action=change_request.decided&projectId=2&pageSize=50"))!;

        page.Items.ShouldNotBeEmpty();
        page.Items.ShouldAllBe(e => e.Action == "change_request.decided" && e.ProjectId == 2);
        page.Items.Select(e => e.OccurredAt).ShouldBe(page.Items.Select(e => e.OccurredAt).OrderDescending());
        page.Items.ShouldAllBe(e => e.ActorName != null && e.CorrelationId != null);
    }
}
