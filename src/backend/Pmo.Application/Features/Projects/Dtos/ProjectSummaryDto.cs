namespace Pmo.Application.Features.Projects.Dtos;

/// <summary>Project details page (B4): delivery, governance and finance numbers as of one date, plus the health badge.</summary>
public record ProjectSummaryDto(
    int ProjectId, DateOnly AsOf, string Health,
    DeliverySummary Delivery, GovernanceSummary Governance, FinanceSummary Finance);
