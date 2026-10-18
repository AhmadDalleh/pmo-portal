using Microsoft.EntityFrameworkCore;
using Pmo.Application.Common;
using Pmo.Application.Common.Abstractions;
using Pmo.Application.Common.Exceptions;

namespace Pmo.Application.Features.Notifications;

public record NotificationDto(long Id, string Title, string Body, string? LinkUrl, DateTimeOffset CreatedAt, DateTimeOffset? ReadAt);
