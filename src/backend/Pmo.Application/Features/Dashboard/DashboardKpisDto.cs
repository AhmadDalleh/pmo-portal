namespace Pmo.Application.Features.Dashboard;

public record DashboardKpisDto(
    int TotalProjects, int ActiveProjects, decimal? PortfolioProgressPct, int OverdueTasks, int MilestonesDueSoon,
    int OpenHighRisks, int BlockedTasks, int PendingChangeRequests,
    decimal CurrentBudget, decimal ActualSpend, decimal RemainingBudget, decimal? Utilization, decimal ForecastToComplete,
    decimal Eac, decimal ProjectedVariance, int ProjectedOverrunProjects, decimal ProjectedOverrunAmount, DateOnly AsOf);
