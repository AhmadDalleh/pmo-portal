using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Pmo.Infrastructure.Persistence;

namespace Pmo.Infrastructure.Identity;

/// <summary>
/// Refresh tokens live in Identity's own table "user_tokens" (no extra table). The browser gets
/// "userId.tokenId.secret" in an HttpOnly cookie; the database keeps only a SHA-256 hash of the secret.
/// Every refresh rotates the token: the old row is deleted, so a stolen copy works at most once.
/// </summary>
public sealed class RefreshTokenService(PmoDbContext db, IOptions<JwtOptions> options, TimeProvider clock)
{
    private const string Provider = "PmoPortal";

    /// <summary>Creates a token for the user and returns the cookie value plus its expiry.</summary>
    public async Task<(string CookieValue, DateTimeOffset ExpiresAt)> IssueAsync(int userId, CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var expires = now.AddDays(options.Value.RefreshTokenDays);
        var tokenId = Guid.NewGuid().ToString("N");
        var secret = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).Replace('+', '-').Replace('/', '_').TrimEnd('=');

        await RemoveExpiredAsync(userId, now, ct);
        db.UserTokens.Add(new IdentityUserToken<int>
        {
            UserId = userId, LoginProvider = Provider, Name = "refresh:" + tokenId,
            Value = $"{Hash(secret)}|{expires.ToUnixTimeSeconds()}"
        });
        await db.SaveChangesAsync(ct);
        return ($"{userId}.{tokenId}.{secret}", expires);
    }

    /// <summary>Checks the cookie, deletes the used token and returns the user id; null when invalid, expired or already used.</summary>
    public async Task<int?> ConsumeAsync(string? cookieValue, CancellationToken ct)
    {
        var token = await FindAsync(cookieValue, ct);
        if (token is null)
        {
            return null;
        }

        db.UserTokens.Remove(token.Value.Row);
        await db.SaveChangesAsync(ct);
        return token.Value.Row.UserId;
    }

    public async Task RevokeAsync(string? cookieValue, CancellationToken ct)
    {
        var token = await FindAsync(cookieValue, ct);
        if (token is not null)
        {
            db.UserTokens.Remove(token.Value.Row);
            await db.SaveChangesAsync(ct);
        }
    }

    private async Task<(IdentityUserToken<int> Row, DateTimeOffset Expires)?> FindAsync(string? cookieValue, CancellationToken ct)
    {
        var parts = cookieValue?.Split('.');
        if (parts is not { Length: 3 } || !int.TryParse(parts[0], out var userId))
        {
            return null;
        }

        var row = await db.UserTokens.FirstOrDefaultAsync(
            t => t.UserId == userId && t.LoginProvider == Provider && t.Name == "refresh:" + parts[1], ct);
        var stored = row?.Value?.Split('|');
        if (row is null || stored is not { Length: 2 } || !long.TryParse(stored[1], out var unix))
        {
            return null;
        }

        var expires = DateTimeOffset.FromUnixTimeSeconds(unix);
        var sameSecret = CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(stored[0]), Encoding.UTF8.GetBytes(Hash(parts[2])));
        return sameSecret && expires > clock.GetUtcNow() ? (row, expires) : null;
    }

    private async Task RemoveExpiredAsync(int userId, DateTimeOffset now, CancellationToken ct)
    {
        var rows = await db.UserTokens.Where(t => t.UserId == userId && t.LoginProvider == Provider).ToListAsync(ct);
        db.UserTokens.RemoveRange(rows.Where(r =>
            long.TryParse(r.Value?.Split('|').ElementAtOrDefault(1), out var unix) && DateTimeOffset.FromUnixTimeSeconds(unix) <= now));
    }

    private static string Hash(string secret) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(secret)));
}
