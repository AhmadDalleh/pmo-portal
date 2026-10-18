namespace Pmo.Application.Features.Finance.Queries;

/// <summary>F5 list filters. From/To are inclusive transaction dates.</summary>
public record CostSearchQuery(string? Status = null, int? CategoryId = null, DateOnly? From = null, DateOnly? To = null, int Page = 1, int PageSize = 20);
