using FluentValidation;
using Pmo.Application.Features.Tasks.Commands;
using Pmo.Application.Features.Tasks.Queries;
using Pmo.Domain.Delivery;

namespace Pmo.Application.Features.Tasks.Validators;

public class UpdateTaskProgressValidator : AbstractValidator<UpdateTaskProgressCommand>
{
    public UpdateTaskProgressValidator()
    {
        RuleFor(x => x.Status).Must(s => ProjectTaskStatus.All.Contains(s)).WithMessage("Unknown status.");
        RuleFor(x => x.ProgressPct).InclusiveBetween(0, 100);
    }
}
