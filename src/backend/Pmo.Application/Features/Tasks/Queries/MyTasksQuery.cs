using System.Linq.Expressions;
using Pmo.Domain.Delivery;

namespace Pmo.Application.Features.Tasks.Queries;

/// <summary>C3. Status null = open tasks (todo, in_progress, blocked); "all" = everything.</summary>
public record MyTasksQuery(string? Status = null, int Page = 1, int PageSize = 20);
