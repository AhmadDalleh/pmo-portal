namespace Pmo.Application.Features.Admin;

public record AuditSearchQuery(
    string? EntityType = null, string? Action = null, int? ActorId = null, int? ProjectId = null, DateOnly? From = null, DateOnly? To = null,
    int Page = 1, int PageSize = 20);
