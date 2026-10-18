using FluentValidation;
using Pmo.Application.Features.Finance.Commands;
using Pmo.Application.Features.Finance.Queries;
using Pmo.Domain.Finance;

namespace Pmo.Application.Features.Finance.Validators;

public class SaveForecastsValidator : AbstractValidator<SaveForecastsCommand>
{
    public SaveForecastsValidator()
    {
        RuleFor(x => x.Items).NotNull().Must(items => items.Count is > 0 and <= 600).WithMessage("Send between 1 and 600 forecast cells.");
        RuleForEach(x => x.Items).ChildRules(item =>
        {
            item.RuleFor(i => i.PeriodMonth.Day).Equal(1).WithMessage("Forecast months must be the first day of the month.");
            item.RuleFor(i => i.Amount).GreaterThanOrEqualTo(0).PrecisionScale(18, 2, false);
        });
    }
}
