using System.Linq.Expressions;
using Pmo.Domain.Delivery;

namespace Pmo.Application.Features.Tasks.Queries;

/// <summary>C1 list filters. Overdue = true keeps only overdue tasks (due before the as-of date, not done or cancelled).</summary>
public record TaskSearchQuery(
    string? Status = null, int? AssigneeId = null, string? Priority = null, bool? Overdue = null,
    string Sort = "due", string Dir = "asc", int Page = 1, int PageSize = 20);
