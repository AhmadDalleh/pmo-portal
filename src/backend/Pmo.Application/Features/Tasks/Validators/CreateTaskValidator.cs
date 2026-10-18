using FluentValidation;
using Pmo.Application.Features.Tasks.Commands;
using Pmo.Application.Features.Tasks.Queries;
using Pmo.Domain.Delivery;

namespace Pmo.Application.Features.Tasks.Validators;

public class CreateTaskValidator : AbstractValidator<CreateTaskCommand>
{
    public CreateTaskValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Status).Must(s => ProjectTaskStatus.All.Contains(s)).WithMessage("Unknown status.");
        RuleFor(x => x.Priority).Must(p => ProjectTaskPriority.All.Contains(p)).WithMessage("Unknown priority.");
        RuleFor(x => x.Weight).InclusiveBetween(1, 10);
        RuleFor(x => x.ProgressPct).InclusiveBetween(0, 100);
        RuleFor(x => x.DueDate).GreaterThanOrEqualTo(x => x.StartDate!.Value).When(x => x.StartDate is not null)
            .WithMessage("Due date must be on or after the start date.");
    }
}
