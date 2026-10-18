namespace Pmo.Application.Features.Risks.Queries;

/// <summary>
/// D2. Status "open" means open or mitigating (the exposure that still counts); "closed" and "all" are also accepted.
/// Null status = open items only. Probability and Impact filter one cell of the risk matrix (D3 drill-down).
/// </summary>
public record RiskSearchQuery(bool? HighExposure = null, string? Status = null, int? ProjectId = null, int? Probability = null, int? Impact = null, int Page = 1, int PageSize = 20);
