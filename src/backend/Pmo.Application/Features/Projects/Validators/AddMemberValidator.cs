using FluentValidation;
using Pmo.Application.Features.Projects.Commands;
using Pmo.Application.Features.Projects.Queries;
using Pmo.Domain.Portfolio;

namespace Pmo.Application.Features.Projects.Validators;

public class AddMemberValidator : AbstractValidator<AddMemberCommand>
{
    public AddMemberValidator()
    {
        RuleFor(x => x.UserId).GreaterThan(0);
        RuleFor(x => x.MemberRole).Must(r => MemberRole.All.Contains(r)).WithMessage("Member role must be 'manager' or 'contributor'.");
    }
}
