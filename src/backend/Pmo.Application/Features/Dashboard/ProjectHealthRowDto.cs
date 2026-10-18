namespace Pmo.Application.Features.Dashboard;

public record ProjectHealthRowDto(
    int ProjectId, string Code, string Name, string Status, decimal? ProgressPct, int OverdueTasks, int OpenHighRisks,
    decimal? Utilization, string Health);
