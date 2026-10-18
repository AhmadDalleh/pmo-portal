using Microsoft.AspNetCore.Identity;
using Pmo.Application.Common;
using Pmo.Application.Common.Abstractions;
using Pmo.Application.Common.Exceptions;
using Pmo.Application.Features.Auth;
using Pmo.Domain.Governance;
using Pmo.Infrastructure.Persistence;

namespace Pmo.Infrastructure.Identity;

/// <summary>
/// Story A1. Checks the password with ASP.NET Core Identity (hashing, lockout after 5 failures for 5 minutes),
/// issues the 15-minute JWT and the rotating refresh cookie, and audits every attempt.
/// Failures never say why (unknown user, wrong password, locked and inactive all look the same).
/// </summary>
public sealed class AuthService(
    UserManager<AppUser> userManager, PmoDbContext db, JwtTokenService jwt, RefreshTokenService refreshTokens,
    TimeProvider clock, ICorrelationIdAccessor correlation)
{
    public async Task<AuthResult> LoginAsync(LoginCommand cmd, CancellationToken ct)
    {
        var user = await userManager.FindByEmailAsync(cmd.Email.Trim());
        if (user is null)
        {
            await AuditAsync("auth.login_failed", null, new { reason = "unknown_user" }, ct);
            throw new AuthenticationFailedException();
        }

        if (!user.IsActive || await userManager.IsLockedOutAsync(user))
        {
            await AuditAsync("auth.login_failed", user.Id, new { reason = user.IsActive ? "locked_out" : "inactive" }, ct);
            throw new AuthenticationFailedException();
        }

        if (!await userManager.CheckPasswordAsync(user, cmd.Password))
        {
            await userManager.AccessFailedAsync(user);   // counts the failure; locks the account on the 5th
            await AuditAsync("auth.login_failed", user.Id, new { reason = "wrong_password" }, ct);
            throw new AuthenticationFailedException();
        }

        await userManager.ResetAccessFailedCountAsync(user);
        await AuditAsync("auth.login_succeeded", user.Id, null, ct);
        return await BuildResultAsync(user, ct);
    }

    /// <summary>Trades a valid refresh cookie for a new access token and a new cookie (rotation).</summary>
    public async Task<AuthResult> RefreshAsync(string? cookieValue, CancellationToken ct)
    {
        var userId = await refreshTokens.ConsumeAsync(cookieValue, ct) ?? throw new AuthenticationFailedException();
        var user = await userManager.FindByIdAsync(userId.ToString());
        if (user is null || !user.IsActive)
        {
            throw new AuthenticationFailedException();
        }

        return await BuildResultAsync(user, ct);
    }

    public Task LogoutAsync(string? cookieValue, CancellationToken ct) => refreshTokens.RevokeAsync(cookieValue, ct);

    private async Task<AuthResult> BuildResultAsync(AppUser user, CancellationToken ct)
    {
        var role = (await userManager.GetRolesAsync(user)).Single();   // one role per user
        var (token, expiresIn) = jwt.Create(user, role);
        var (cookie, cookieExpires) = await refreshTokens.IssueAsync(user.Id, ct);
        var managed = await ProjectAccess.ManagedIdsAsync(db, user.Id, role, ct);
        var me = new MeDto(user.Id, user.Email!, user.FullName, role, user.DepartmentId, managed);
        return new AuthResult(new LoginResponse(token, expiresIn, me), cookie, cookieExpires);
    }

    private async Task AuditAsync(string action, int? userId, object? data, CancellationToken ct)
    {
        db.AuditEvents.Add(AuditEvent.Create(action, "User", userId?.ToString() ?? "unknown", userId, null,
            correlation.CorrelationId, clock.GetUtcNow(), data));
        await db.SaveChangesAsync(ct);
    }
}
