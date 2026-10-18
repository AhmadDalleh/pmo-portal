using FluentValidation;
using Pmo.Application.Features.Milestones.Commands;

namespace Pmo.Application.Features.Milestones.Validators;

public class UpdateMilestoneValidator : AbstractValidator<UpdateMilestoneCommand>
{
    public UpdateMilestoneValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
    }
}
