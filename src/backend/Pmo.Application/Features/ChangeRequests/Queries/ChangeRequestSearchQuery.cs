namespace Pmo.Application.Features.ChangeRequests.Queries;

/// <summary>Used by the project tab (all statuses unless filtered) and by the review queue (status defaults to "submitted").</summary>
public record ChangeRequestSearchQuery(string? Status = null, int? ProjectId = null, int Page = 1, int PageSize = 20);
