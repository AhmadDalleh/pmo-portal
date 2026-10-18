namespace Pmo.Application.Features.Dashboard;

/// <summary>Drill-down options for the risk list (G4, D3 matrix cell).</summary>
public record RiskDrilldownOptions(int? Probability = null, int? Impact = null, bool HighExposure = false, int Page = 1, int PageSize = 20);
