namespace Pmo.Application.Features.Finance.Dtos;

public record BudgetRevisionDto(
    int Id, int ProjectId, decimal Amount, string Reason, DateOnly EffectiveDate, string Status, int? ChangeRequestId,
    string RequestedByName, DateTimeOffset RequestedAt, string? DecidedByName, DateTimeOffset? DecidedAt, string? DecisionReason, uint Version);
