using FluentValidation;
using Pmo.Application.Features.Finance.Commands;
using Pmo.Application.Features.Finance.Queries;
using Pmo.Domain.Finance;

namespace Pmo.Application.Features.Finance.Validators;

public class DecideRevisionValidator : AbstractValidator<DecideRevisionCommand>
{
    public DecideRevisionValidator()
    {
        RuleFor(x => x.Decision).Must(d => d is RevisionStatus.Approved or RevisionStatus.Rejected).WithMessage("Decision must be 'approved' or 'rejected'.");
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(1000);
    }
}
