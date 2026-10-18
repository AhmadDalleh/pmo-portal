namespace Pmo.Application.Features.ChangeRequests.Commands;

public record UpdateChangeRequestCommand(string Title, string Description, string Type, decimal? CostImpact, int? ScheduleImpactDays, uint Version);
