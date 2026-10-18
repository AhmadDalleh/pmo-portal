using Pmo.Application.Features.Projects.Queries;

namespace Pmo.Application.Features.Projects;

/// <summary>
/// The health badge rule of story B4, in one place so it can be unit tested and quoted in the interview.
/// Red: overspent, projected overrun or 3+ overdue tasks.
/// Amber: utilisation 90% or more, any overdue task, or an open high-exposure risk.
/// Green: none of the above.
/// </summary>
public static class ProjectHealth
{
    public const string Red = "red";
    public const string Amber = "amber";
    public const string Green = "green";

    public static string Evaluate(ProjectKpiRow kpi) => Evaluate(
        kpi.Overspent, kpi.ProjectedOverrun, kpi.OverdueTasks, kpi.Utilization, kpi.OpenHighRisks);

    public static string Evaluate(bool overspent, bool projectedOverrun, int overdueTasks, decimal? utilization, int openHighRisks)
    {
        if (overspent || projectedOverrun || overdueTasks >= 3)
        {
            return Red;
        }

        if (utilization >= 0.90m || overdueTasks > 0 || openHighRisks > 0)
        {
            return Amber;
        }

        return Green;
    }
}
