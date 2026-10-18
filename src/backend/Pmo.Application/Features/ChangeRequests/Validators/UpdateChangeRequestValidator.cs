using FluentValidation;
using Pmo.Application.Features.ChangeRequests.Commands;
using Pmo.Application.Features.ChangeRequests.Queries;
using Pmo.Domain.Governance;

namespace Pmo.Application.Features.ChangeRequests.Validators;

public class UpdateChangeRequestValidator : AbstractValidator<UpdateChangeRequestCommand>
{
    public UpdateChangeRequestValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Description).NotEmpty().MaximumLength(4000);
        RuleFor(x => x.Type).Must(t => ChangeRequestType.All.Contains(t)).WithMessage("Type must be scope, schedule, budget or resource.");
        RuleFor(x => x.CostImpact).PrecisionScale(18, 2, false);
        RuleFor(x => x.ScheduleImpactDays).InclusiveBetween(-3650, 3650);
    }
}
