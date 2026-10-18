using FluentValidation;
using Pmo.Application.Features.Finance.Commands;
using Pmo.Application.Features.Finance.Queries;
using Pmo.Domain.Finance;

namespace Pmo.Application.Features.Finance.Validators;

public class RequestRevisionValidator : AbstractValidator<RequestRevisionCommand>
{
    public RequestRevisionValidator()
    {
        RuleFor(x => x.Amount).NotEqual(0).PrecisionScale(18, 2, false);
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(1000);
    }
}
