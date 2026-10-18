namespace Pmo.Application.Features.Projects.Queries;

/// <summary>
/// One project with its KPIs, exactly as SQL returned them (columns are snake_case, mapped by Dapper).
/// Settable properties because Dapper fills them one by one.
/// </summary>
public class ProjectKpiRow
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string DepartmentName { get; set; } = string.Empty;
    public string OwnerName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateOnly StartDate { get; set; }
    public DateOnly PlannedEndDate { get; set; }

    public decimal? ProgressPct { get; set; }
    public int OverdueTasks { get; set; }
    public int OpenHighRisks { get; set; }
    public int PendingChangeRequests { get; set; }

    public decimal CurrentBudget { get; set; }
    public decimal ActualSpend { get; set; }
    public decimal RemainingBudget { get; set; }
    public decimal? Utilization { get; set; }
    public decimal ForecastToComplete { get; set; }
    public decimal Eac { get; set; }
    public decimal ProjectedVariance { get; set; }

    public bool NearBudget { get; set; }
    public bool Overspent { get; set; }
    public bool ProjectedOverrun { get; set; }
    public bool NoForecast { get; set; }
    public bool NoBudget { get; set; }

    /// <summary>Total rows matching the filters (same value on every row of the page).</summary>
    public int TotalCount { get; set; }
}
