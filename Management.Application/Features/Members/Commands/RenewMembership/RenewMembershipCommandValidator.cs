using FluentValidation;

namespace Management.Application.Features.Members.Commands.RenewMembership
{
    public class RenewMembershipCommandValidator : AbstractValidator<RenewMembershipCommand>
    {
        public RenewMembershipCommandValidator()
        {
            RuleFor(x => x.MemberIds)
                .NotEmpty().WithMessage("At least one Member ID is required for renewal.");
        }
    }
}
