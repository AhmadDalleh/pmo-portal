using FluentValidation;
using Pmo.Application.Features.Finance.Commands;
using Pmo.Application.Features.Finance.Queries;
using Pmo.Domain.Finance;

namespace Pmo.Application.Features.Finance.Validators;

public class CreateCostValidator : AbstractValidator<CreateCostCommand>
{
    public CreateCostValidator()
    {
        RuleFor(x => x.CategoryId).GreaterThan(0);
        RuleFor(x => x.Amount).GreaterThan(0).PrecisionScale(18, 2, false);
        RuleFor(x => x.Description).NotEmpty().MaximumLength(300);
        RuleFor(x => x.VendorName).MaximumLength(150);
        RuleFor(x => x.ReferenceNo).MaximumLength(50);
    }
}
