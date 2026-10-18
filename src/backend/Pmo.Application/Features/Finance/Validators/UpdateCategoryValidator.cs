using FluentValidation;
using Pmo.Application.Features.Finance.Commands;
using Pmo.Application.Features.Finance.Queries;
using Pmo.Domain.Finance;

namespace Pmo.Application.Features.Finance.Validators;

public class UpdateCategoryValidator : AbstractValidator<UpdateCategoryCommand>
{
    public UpdateCategoryValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
    }
}
