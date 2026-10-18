using FluentValidation;
using Pmo.Application.Features.ChangeRequests.Commands;
using Pmo.Application.Features.ChangeRequests.Queries;
using Pmo.Domain.Governance;

namespace Pmo.Application.Features.ChangeRequests.Validators;

public class DecideChangeRequestValidator : AbstractValidator<DecideChangeRequestCommand>
{
    public DecideChangeRequestValidator()
    {
        RuleFor(x => x.Decision).Must(d => d is "approve" or "reject" or "return").WithMessage("Decision must be approve, reject or return.");
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(1000);
    }
}
