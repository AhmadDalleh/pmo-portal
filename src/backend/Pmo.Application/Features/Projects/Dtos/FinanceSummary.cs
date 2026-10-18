namespace Pmo.Application.Features.Projects.Dtos;

public record FinanceSummary(
    decimal CurrentBudget, decimal ActualSpend, decimal RemainingBudget, decimal? Utilization,
    decimal ForecastToComplete, decimal Eac, decimal ProjectedVariance,
    bool NearBudget, bool Overspent, bool ProjectedOverrun, bool NoForecast, bool NoBudget);
