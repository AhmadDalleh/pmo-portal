namespace Pmo.Application.Common;

/// <summary>One page of a list plus the total number of rows that match the filters.</summary>
public record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount);
