namespace Pmo.Application.Features.Projects.Dtos;

/// <summary>One row of the portfolio list (B3). Every number comes from SQL (database-design.md §6).</summary>
public record ProjectListItemDto(
    int Id, string Code, string Name, string DepartmentName, string OwnerName, string Status,
    DateOnly StartDate, DateOnly PlannedEndDate,
    decimal? ProgressPct, decimal? Utilization, string Health,
    bool NearBudget, bool Overspent, bool ProjectedOverrun, bool NoForecast);
