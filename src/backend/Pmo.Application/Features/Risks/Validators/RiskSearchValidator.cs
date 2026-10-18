using FluentValidation;
using Pmo.Application.Features.Risks.Commands;
using Pmo.Application.Features.Risks.Queries;
using Pmo.Domain.Delivery;

namespace Pmo.Application.Features.Risks.Validators;

public class RiskSearchValidator : AbstractValidator<RiskSearchQuery>
{
    public RiskSearchValidator()
    {
        RuleFor(x => x.Status).Must(s => s is null or "open" or "closed" or "all").WithMessage("Status must be open, closed or all.");
        RuleFor(x => x.Probability).InclusiveBetween(1, 5).When(x => x.Probability is not null);
        RuleFor(x => x.Impact).InclusiveBetween(1, 5).When(x => x.Impact is not null);
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
    }
}
