namespace Pmo.Application.Features.Projects.Queries;

/// <summary>B3 filters. Dir is "asc" or "desc". The date range keeps projects that overlap [From, To].</summary>
public record ProjectSearchQuery(
    string? Search = null, int? DepartmentId = null, int? OwnerId = null, string? Status = null,
    DateOnly? From = null, DateOnly? To = null, string Sort = "code", string Dir = "asc",
    int Page = 1, int PageSize = 20);
