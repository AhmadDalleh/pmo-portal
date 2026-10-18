namespace Pmo.Application.Features.Dashboard;

/// <summary>
/// The filters every dashboard call carries (story G, shared by KPIs, charts and drill-downs).
/// ManagerId = project owner. From/To keep projects whose start..planned-end overlaps the window.
/// CategoryId only affects the monthly trend, spend by category and the cost drill-down.
/// </summary>
public record DashboardFilter(
    int? DepartmentId = null, int? ProjectId = null, int? ManagerId = null, string? Status = null,
    DateOnly? From = null, DateOnly? To = null, int? CategoryId = null, DateOnly? AsOf = null);
