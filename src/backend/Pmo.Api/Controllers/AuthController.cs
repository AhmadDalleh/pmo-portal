using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Pmo.Application.Common;
using Pmo.Application.Common.Abstractions;
using Pmo.Application.Features.Auth;
using Pmo.Infrastructure.Identity;

namespace Pmo.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController(AuthService auth, IPmoDbContext db, ICurrentUser currentUser, IUserDirectory users) : ControllerBase
{
    public const string RefreshCookie = "pmo_refresh";

    /// <summary>A1. Returns the access token in the body and sets the refresh token as an HttpOnly cookie.</summary>
    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting("login")]
    [ProducesResponseType<LoginResponse>(200)]
    [ProducesResponseType<ProblemDetails>(400)]
    [ProducesResponseType<ProblemDetails>(401)]
    public async Task<ActionResult<LoginResponse>> Login(LoginCommand body, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(body.Email) || string.IsNullOrEmpty(body.Password))
        {
            return Problem(statusCode: 400, title: "Invalid request", detail: "Email and password are required.");
        }

        var result = await auth.LoginAsync(body, ct);
        SetRefreshCookie(result);
        return Ok(result.Body);
    }

    /// <summary>Uses the refresh cookie to issue a new access token. 401 when the cookie is missing, used, expired or revoked.</summary>
    [HttpPost("refresh")]
    [AllowAnonymous]
    [ProducesResponseType<LoginResponse>(200)]
    [ProducesResponseType<ProblemDetails>(401)]
    public async Task<ActionResult<LoginResponse>> Refresh(CancellationToken ct)
    {
        var result = await auth.RefreshAsync(Request.Cookies[RefreshCookie], ct);
        SetRefreshCookie(result);
        return Ok(result.Body);
    }

    [HttpPost("logout")]
    [AllowAnonymous]
    [ProducesResponseType(204)]
    public async Task<IActionResult> Logout(CancellationToken ct)
    {
        await auth.LogoutAsync(Request.Cookies[RefreshCookie], ct);
        Response.Cookies.Delete(RefreshCookie, CookieOptions(null));
        return NoContent();
    }

    /// <summary>A2. The caller's profile; the UI builds its menus from it.</summary>
    [HttpGet("me")]
    [ProducesResponseType<MeDto>(200)]
    [ProducesResponseType<ProblemDetails>(401)]
    public async Task<ActionResult<MeDto>> Me(CancellationToken ct)
    {
        var person = await users.FindAsync(currentUser.Id, ct);
        if (person is null || !person.IsActive)
        {
            return Unauthorized();
        }

        var managed = await ProjectAccess.ManagedIdsAsync(db, person.Id, person.Role, ct);
        return Ok(new MeDto(person.Id, person.Email, person.FullName, person.Role, person.DepartmentId, managed));
    }

    private void SetRefreshCookie(AuthResult result) =>
        Response.Cookies.Append(RefreshCookie, result.RefreshCookieValue, CookieOptions(result.RefreshExpiresAt));

    // HttpOnly: JavaScript cannot read it. Strict: never sent from another site. Path: only the auth endpoints receive it.
    private static CookieOptions CookieOptions(DateTimeOffset? expires) => new()
    {
        HttpOnly = true, Secure = true, SameSite = SameSiteMode.Strict, Path = "/api/auth", Expires = expires
    };
}
