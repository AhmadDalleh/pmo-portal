namespace Pmo.Application.Features.Finance.Dtos;

/// <summary>F1. Budget breakdown for one project as of a date. Every figure is calculated by SQL (database-design.md section 6).</summary>
public record FinanceSummaryDto(
    int ProjectId, DateOnly AsOf, string Currency, decimal InitialBudget, decimal ApprovedRevisions, decimal CurrentBudget,
    decimal ActualSpend, decimal RemainingBudget, decimal? Utilization, decimal ForecastToComplete, decimal Eac, decimal ProjectedVariance,
    bool NearBudget, bool Overspent, bool ProjectedOverrun, bool NoForecast, bool NoBudget, int PendingRevisions);
