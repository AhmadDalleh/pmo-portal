using FluentValidation;
using Pmo.Application.Features.Tasks.Commands;
using Pmo.Application.Features.Tasks.Queries;
using Pmo.Domain.Delivery;

namespace Pmo.Application.Features.Tasks.Validators;

public class TaskSearchValidator : AbstractValidator<TaskSearchQuery>
{
    public static readonly string[] SortKeys = ["due", "title", "status", "priority", "progress", "weight"];

    public TaskSearchValidator()
    {
        RuleFor(x => x.Sort).Must(s => SortKeys.Contains(s)).WithMessage($"Sort must be one of: {string.Join(", ", SortKeys)}.");
        RuleFor(x => x.Dir).Must(d => d is "asc" or "desc").WithMessage("Dir must be 'asc' or 'desc'.");
        RuleFor(x => x.Status).Must(s => s is null || ProjectTaskStatus.All.Contains(s)).WithMessage("Unknown status.");
        RuleFor(x => x.Priority).Must(p => p is null || ProjectTaskPriority.All.Contains(p)).WithMessage("Unknown priority.");
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
    }
}
