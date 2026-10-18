using FluentValidation;
using Pmo.Application.Features.Finance.Commands;
using Pmo.Application.Features.Finance.Queries;
using Pmo.Domain.Finance;

namespace Pmo.Application.Features.Finance.Validators;

public class CostSearchValidator : AbstractValidator<CostSearchQuery>
{
    public CostSearchValidator()
    {
        RuleFor(x => x.Status).Must(s => s is null || CostStatus.All.Contains(s)).WithMessage("Status must be 'draft' or 'posted'.");
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
        RuleFor(x => x.To).GreaterThanOrEqualTo(x => x.From).When(x => x.From is not null && x.To is not null);
    }
}
