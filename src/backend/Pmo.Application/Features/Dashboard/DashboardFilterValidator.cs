using FluentValidation;
using Pmo.Domain.Portfolio;

namespace Pmo.Application.Features.Dashboard;

public class DashboardFilterValidator : AbstractValidator<DashboardFilter>
{
    public DashboardFilterValidator()
    {
        RuleFor(x => x.Status).Must(s => s is null || ProjectStatus.All.Contains(s)).WithMessage("Unknown status.");
        RuleFor(x => x.To).GreaterThanOrEqualTo(x => x.From).When(x => x.From is not null && x.To is not null)
            .WithMessage("The end of the timeframe must be on or after its start.");
    }
}
