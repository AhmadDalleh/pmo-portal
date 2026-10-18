using FluentValidation;
using Pmo.Application.Features.ChangeRequests.Commands;
using Pmo.Application.Features.ChangeRequests.Queries;
using Pmo.Domain.Governance;

namespace Pmo.Application.Features.ChangeRequests.Validators;

public class ChangeRequestSearchValidator : AbstractValidator<ChangeRequestSearchQuery>
{
    public ChangeRequestSearchValidator()
    {
        RuleFor(x => x.Status).Must(s => s is null || ChangeRequestStatus.All.Contains(s)).WithMessage("Unknown status.");
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
    }
}
