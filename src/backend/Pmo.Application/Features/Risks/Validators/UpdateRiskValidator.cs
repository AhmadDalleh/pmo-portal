using FluentValidation;
using Pmo.Application.Features.Risks.Commands;
using Pmo.Application.Features.Risks.Queries;
using Pmo.Domain.Delivery;

namespace Pmo.Application.Features.Risks.Validators;

public class UpdateRiskValidator : AbstractValidator<UpdateRiskCommand>
{
    public UpdateRiskValidator()
    {
        RuleFor(x => x.Type).Must(t => RiskType.All.Contains(t)).WithMessage("Type must be 'risk' or 'issue'.");
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
        RuleFor(x => x.OwnerId).GreaterThan(0);
        RuleFor(x => x.Probability).InclusiveBetween(1, 5);
        RuleFor(x => x.Impact).InclusiveBetween(1, 5);
        RuleFor(x => x.Status).Must(s => RiskStatus.All.Contains(s)).WithMessage("Unknown status.");
        RuleFor(x => x.Mitigation).NotEmpty().When(x => x.Probability * x.Impact >= Risk.HighExposureThreshold)
            .WithMessage($"A mitigation plan is required when probability x impact is {Risk.HighExposureThreshold} or more.");
    }
}
