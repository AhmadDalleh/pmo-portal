using FluentValidation;
using Pmo.Application.Features.Projects.Commands;
using Pmo.Application.Features.Projects.Queries;
using Pmo.Domain.Portfolio;

namespace Pmo.Application.Features.Projects.Validators;

public class UpdateProjectValidator : AbstractValidator<UpdateProjectCommand>
{
    public UpdateProjectValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.OwnerId).GreaterThan(0);
        RuleFor(x => x.Status).Must(s => ProjectStatus.All.Contains(s)).WithMessage("Unknown status.");
        RuleFor(x => x.PlannedEndDate).GreaterThanOrEqualTo(x => x.StartDate).WithMessage("Planned end date must be on or after the start date.");
        RuleFor(x => x.ActualEndDate).NotNull().When(x => x.Status == ProjectStatus.Completed)
            .WithMessage("A completed project needs an actual end date.");
    }
}
