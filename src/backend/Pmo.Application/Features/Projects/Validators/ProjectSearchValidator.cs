using FluentValidation;
using Pmo.Application.Features.Projects.Commands;
using Pmo.Application.Features.Projects.Queries;
using Pmo.Domain.Portfolio;

namespace Pmo.Application.Features.Projects.Validators;

public class ProjectSearchValidator : AbstractValidator<ProjectSearchQuery>
{
    /// <summary>The only sort keys the API accepts; the SQL maps each one to a fixed column (never user text).</summary>
    public static readonly string[] SortKeys = ["code", "name", "department", "owner", "status", "start", "end", "progress", "utilization"];

    public ProjectSearchValidator()
    {
        RuleFor(x => x.Sort).Must(s => SortKeys.Contains(s)).WithMessage($"Sort must be one of: {string.Join(", ", SortKeys)}.");
        RuleFor(x => x.Dir).Must(d => d is "asc" or "desc").WithMessage("Dir must be 'asc' or 'desc'.");
        RuleFor(x => x.Status).Must(s => s is null || ProjectStatus.All.Contains(s)).WithMessage("Unknown status.");
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
        RuleFor(x => x.To).GreaterThanOrEqualTo(x => x.From).When(x => x.From is not null && x.To is not null);
    }
}
