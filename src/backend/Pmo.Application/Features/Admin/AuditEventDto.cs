namespace Pmo.Application.Features.Admin;

public record AuditEventDto(
    long Id, DateTimeOffset OccurredAt, int? ActorId, string? ActorName, int? ProjectId, string EntityType, string EntityId, string Action,
    string? CorrelationId, string? Data);
