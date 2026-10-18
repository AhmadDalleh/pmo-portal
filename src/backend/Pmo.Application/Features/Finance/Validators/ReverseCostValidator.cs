using FluentValidation;
using Pmo.Application.Features.Finance.Commands;
using Pmo.Application.Features.Finance.Queries;
using Pmo.Domain.Finance;

namespace Pmo.Application.Features.Finance.Validators;

public class ReverseCostValidator : AbstractValidator<ReverseCostCommand>
{
    public ReverseCostValidator()
    {
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(250);
    }
}
