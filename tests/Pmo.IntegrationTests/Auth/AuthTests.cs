using System.Net;
using Microsoft.EntityFrameworkCore;
using System.Net.Http.Json;
using System.Text.Json;
using Pmo.Application.Features.Auth;
using Pmo.IntegrationTests.Infrastructure;
using Shouldly;

namespace Pmo.IntegrationTests.Auth;

/// <summary>Story A1/A2: sign in, refresh cookie, lockout, /me.</summary>
[Collection(ApiCollection.Name)]
public class AuthTests(PmoApiFactory factory)
{
    private static Task<HttpResponseMessage> LoginAsync(HttpClient client, string email, string password) =>
        client.PostAsJsonAsync("/api/auth/login", new LoginCommand(email, password));

    private static string RefreshCookieFrom(HttpResponseMessage response) =>
        response.Headers.GetValues("Set-Cookie").Single(c => c.StartsWith("pmo_refresh=", StringComparison.Ordinal));

    private static HttpRequestMessage RefreshRequest(string setCookie)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/refresh");
        request.Headers.Add("Cookie", setCookie.Split(';')[0]);
        return request;
    }

    [Fact]
    public async Task Login_ValidCredentials_ReturnsTokenAndHardenedRefreshCookie()
    {
        var response = await LoginAsync(factory.CreateClient(), DemoUsers.Pm1, PmoApiFactory.DemoPassword);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<LoginResponse>();
        body!.AccessToken.ShouldNotBeNullOrWhiteSpace();
        body.ExpiresInSeconds.ShouldBe(900);
        body.User.Role.ShouldBe("ProjectManager");

        var cookie = RefreshCookieFrom(response).ToLowerInvariant();
        cookie.ShouldContain("httponly");
        cookie.ShouldContain("secure");
        cookie.ShouldContain("samesite=strict");
        cookie.ShouldContain("path=/api/auth");
        JsonSerializer.Serialize(body).ShouldNotContain("refresh");   // the refresh token never appears in the body
    }

    [Fact]
    public async Task Login_WrongPassword_Returns401WithGenericMessageAndWritesAudit()
    {
        var wrongPassword = await LoginAsync(factory.CreateClient(), DemoUsers.Pm2, "not-the-password");
        var unknownUser = await LoginAsync(factory.CreateClient(), "nobody@pmo.example", "whatever");

        wrongPassword.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        unknownUser.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        var detail1 = (await wrongPassword.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("detail").GetString();
        var detail2 = (await unknownUser.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("detail").GetString();
        detail1.ShouldBe(detail2);   // no "this user exists" hint

        var audited = await factory.ScalarAsync<long>(
            "SELECT count(*) FROM audit_events WHERE action = 'auth.login_failed' AND data->>'reason' = 'wrong_password'");
        audited.ShouldBeGreaterThanOrEqualTo(1);
    }

    [Fact]
    public async Task Login_AfterFiveWrongPasswords_LocksAccountEvenForCorrectPassword()
    {
        // con10 is reserved for this test; the lockout is cleared at the end.
        var client = factory.CreateClient();
        for (var i = 0; i < 5; i++)
        {
            (await LoginAsync(client, "con10@pmo.example", "wrong")).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        }

        var correctButLocked = await LoginAsync(client, "con10@pmo.example", PmoApiFactory.DemoPassword);

        correctButLocked.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        await using var db = factory.CreateDbContext();
        await db.Database.ExecuteSqlRawAsync("UPDATE users SET lockout_end = NULL, access_failed_count = 0 WHERE email = 'con10@pmo.example'");
    }

    [Fact]
    public async Task Login_InactiveUser_Returns401()
    {
        // con9 is reserved for this test.
        await using var db = factory.CreateDbContext();
        await db.Database.ExecuteSqlRawAsync("UPDATE users SET is_active = false WHERE email = 'con9@pmo.example'");
        try
        {
            var response = await LoginAsync(factory.CreateClient(), "con9@pmo.example", PmoApiFactory.DemoPassword);

            response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        }
        finally
        {
            await db.Database.ExecuteSqlRawAsync("UPDATE users SET is_active = true WHERE email = 'con9@pmo.example'");
        }
    }

    [Fact]
    public async Task Refresh_WithValidCookie_IssuesNewTokenAndRejectsTheUsedCookie()
    {
        var login = await LoginAsync(factory.CreateClient(), DemoUsers.Pm3, PmoApiFactory.DemoPassword);
        var firstCookie = RefreshCookieFrom(login);

        var refreshed = await factory.CreateClient().SendAsync(RefreshRequest(firstCookie));
        var reused = await factory.CreateClient().SendAsync(RefreshRequest(firstCookie));

        refreshed.StatusCode.ShouldBe(HttpStatusCode.OK);
        RefreshCookieFrom(refreshed).ShouldNotBe(firstCookie);   // rotated
        reused.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Logout_RevokesTheRefreshCookie()
    {
        var login = await LoginAsync(factory.CreateClient(), DemoUsers.Pm4, PmoApiFactory.DemoPassword);
        var cookie = RefreshCookieFrom(login);
        var logout = new HttpRequestMessage(HttpMethod.Post, "/api/auth/logout");
        logout.Headers.Add("Cookie", cookie.Split(';')[0]);

        (await factory.CreateClient().SendAsync(logout)).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        var refresh = await factory.CreateClient().SendAsync(RefreshRequest(cookie));

        refresh.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Me_AsProjectManager_ListsOnlyProjectsTheyManage()
    {
        var client = await factory.CreateClientForAsync(DemoUsers.Pm1);

        var me = await client.GetFromJsonAsync<MeDto>("/api/auth/me");

        me!.Role.ShouldBe("ProjectManager");
        me.ManagedProjectIds.Length.ShouldBe(3);   // PRJ-001, PRJ-006, PRJ-009
    }

    [Fact]
    public async Task Me_AsContributor_ManagesNothing()
    {
        var client = await factory.CreateClientForAsync(DemoUsers.Con1);

        var me = await client.GetFromJsonAsync<MeDto>("/api/auth/me");

        me!.ManagedProjectIds.ShouldBeEmpty();
    }

    [Fact]
    public async Task ProtectedRoute_WithoutToken_Returns401()
    {
        var response = await factory.CreateClient().GetAsync("/api/projects");

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ProtectedRoute_WithGarbageToken_Returns401()
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", "not.a.jwt");

        (await client.GetAsync("/api/projects")).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }
}
