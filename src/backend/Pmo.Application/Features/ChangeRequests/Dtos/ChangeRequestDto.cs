namespace Pmo.Application.Features.ChangeRequests.Dtos;

public record ChangeRequestDto(
    int Id, string Code, int ProjectId, string ProjectCode, string Title, string Description, string Type, decimal? CostImpact,
    int? ScheduleImpactDays, string Status, int RequestedById, string RequestedByName, DateTimeOffset CreatedAt, DateTimeOffset? SubmittedAt,
    string? DecidedByName, DateTimeOffset? DecidedAt, string? DecisionReason, uint Version);
