using FluentValidation;
using Pmo.Application.Features.Tasks.Commands;
using Pmo.Application.Features.Tasks.Queries;
using Pmo.Domain.Delivery;

namespace Pmo.Application.Features.Tasks.Validators;

public class MyTasksValidator : AbstractValidator<MyTasksQuery>
{
    public MyTasksValidator()
    {
        RuleFor(x => x.Status).Must(s => s is null or "all" || ProjectTaskStatus.All.Contains(s)).WithMessage("Unknown status.");
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
    }
}
