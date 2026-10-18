namespace Pmo.Application.Features.Projects.Dtos;

public record DeliverySummary(decimal? ProgressPct, int OverdueTasks, NextMilestoneDto? NextMilestone);
