namespace Pmo.Application.Features.ChangeRequests.Commands;

/// <summary>E1. CostImpact is informational only: approving a change request never changes the budget by itself.</summary>
public record CreateChangeRequestCommand(string Title, string Description, string Type, decimal? CostImpact, int? ScheduleImpactDays);
