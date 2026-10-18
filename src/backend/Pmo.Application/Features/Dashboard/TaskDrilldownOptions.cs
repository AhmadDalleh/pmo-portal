namespace Pmo.Application.Features.Dashboard;

/// <summary>Drill-down options for the task list (G4).</summary>
public record TaskDrilldownOptions(bool Overdue = true, int? AssigneeId = null, bool Unassigned = false, int Page = 1, int PageSize = 20);
