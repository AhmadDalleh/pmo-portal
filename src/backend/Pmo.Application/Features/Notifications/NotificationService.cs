using Microsoft.EntityFrameworkCore;
using Pmo.Application.Common;
using Pmo.Application.Common.Abstractions;
using Pmo.Application.Common.Exceptions;

namespace Pmo.Application.Features.Notifications;

/// <summary>Story A6: the signed-in user's own in-app notifications. Someone else's notification is a 404, never a 403.</summary>
public sealed class NotificationService(IPmoDbContext db, ICurrentUser user, TimeProvider clock)
{
    public async Task<PagedResult<NotificationDto>> ListAsync(bool unreadOnly, int page, int pageSize, CancellationToken ct)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var mine = db.Notifications.AsNoTracking().Where(n => n.UserId == user.Id);
        if (unreadOnly) mine = mine.Where(n => n.ReadAt == null);

        var total = await mine.CountAsync(ct);
        var rows = await mine.OrderByDescending(n => n.CreatedAt).ThenByDescending(n => n.Id)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(n => new NotificationDto(n.Id, n.Title, n.Body, n.LinkUrl, n.CreatedAt, n.ReadAt)).ToListAsync(ct);
        return new PagedResult<NotificationDto>(rows, page, pageSize, total);
    }

    /// <summary>For the bell icon.</summary>
    public Task<int> UnreadCountAsync(CancellationToken ct) => db.Notifications.CountAsync(n => n.UserId == user.Id && n.ReadAt == null, ct);

    public async Task<NotificationDto> MarkReadAsync(long id, CancellationToken ct)
    {
        var n = await db.Notifications.FirstOrDefaultAsync(x => x.Id == id && x.UserId == user.Id, ct) ?? throw new NotFoundException("Notification", id);
        n.ReadAt ??= clock.GetUtcNow();
        await db.SaveChangesAsync(ct);
        return new NotificationDto(n.Id, n.Title, n.Body, n.LinkUrl, n.CreatedAt, n.ReadAt);
    }
}
