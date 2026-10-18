using FluentValidation;
using Pmo.Application.Features.Projects.Commands;
using Pmo.Application.Features.Projects.Queries;
using Pmo.Domain.Portfolio;

namespace Pmo.Application.Features.Projects.Validators;

public class CreateProjectValidator : AbstractValidator<CreateProjectCommand>
{
    public CreateProjectValidator()
    {
        RuleFor(x => x.Code).NotEmpty().MaximumLength(20).Matches("^[A-Za-z0-9-]+$").WithMessage("Code may contain letters, digits and '-' only.");
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.DepartmentId).GreaterThan(0);
        RuleFor(x => x.PlannedEndDate).GreaterThanOrEqualTo(x => x.StartDate).WithMessage("Planned end date must be on or after the start date.");
        RuleFor(x => x.InitialBudget).GreaterThanOrEqualTo(0).PrecisionScale(18, 2, false);
    }
}
