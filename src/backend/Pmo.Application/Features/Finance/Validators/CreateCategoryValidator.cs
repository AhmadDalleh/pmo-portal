using FluentValidation;
using Pmo.Application.Features.Finance.Commands;
using Pmo.Application.Features.Finance.Queries;
using Pmo.Domain.Finance;

namespace Pmo.Application.Features.Finance.Validators;

public class CreateCategoryValidator : AbstractValidator<CreateCategoryCommand>
{
    public CreateCategoryValidator()
    {
        RuleFor(x => x.Code).NotEmpty().MaximumLength(20).Matches("^[A-Za-z0-9_]+$");
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
    }
}
