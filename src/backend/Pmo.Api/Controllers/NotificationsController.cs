using Microsoft.AspNetCore.Mvc;
using Pmo.Application.Common;
using Pmo.Application.Features.Notifications;

namespace Pmo.Api.Controllers;

/// <summary>A6. Always the caller's own notifications.</summary>
[ApiController]
[Route("api/notifications")]
public class NotificationsController(NotificationService notifications) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<PagedResult<NotificationDto>>(200)]
    public async Task<ActionResult<PagedResult<NotificationDto>>> List(
        [FromQuery] bool unread = false, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
        => Ok(await notifications.ListAsync(unread, page, pageSize, ct));

    [HttpGet("unread-count")]
    [ProducesResponseType<int>(200)]
    public async Task<ActionResult<int>> UnreadCount(CancellationToken ct) => Ok(await notifications.UnreadCountAsync(ct));

    [HttpPost("{id:long}/read")]
    [ProducesResponseType<NotificationDto>(200)]
    [ProducesResponseType<ProblemDetails>(404)]
    public async Task<ActionResult<NotificationDto>> MarkRead(long id, CancellationToken ct) => Ok(await notifications.MarkReadAsync(id, ct));
}
