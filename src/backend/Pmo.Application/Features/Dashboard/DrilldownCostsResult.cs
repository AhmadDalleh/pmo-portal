namespace Pmo.Application.Features.Dashboard;

/// <summary>Page of posted transactions plus the sum of ALL matching rows, which equals the KPI or chart value it was opened from.</summary>
public record DrilldownCostsResult(IReadOnlyList<DrilldownCostDto> Items, int Page, int PageSize, int TotalCount, decimal TotalAmount);
