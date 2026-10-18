using FluentValidation;
using Pmo.Application.Features.Milestones.Commands;

namespace Pmo.Application.Features.Milestones.Validators;

public class CreateMilestoneValidator : AbstractValidator<CreateMilestoneCommand>
{
    public CreateMilestoneValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
    }
}
