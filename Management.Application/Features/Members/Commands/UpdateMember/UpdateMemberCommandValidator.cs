using FluentValidation;

namespace Management.Application.Features.Members.Commands.UpdateMember
{
    public class UpdateMemberCommandValidator : AbstractValidator<UpdateMemberCommand>
    {
        public UpdateMemberCommandValidator()
        {
            RuleFor(x => x.Member).NotNull();
            RuleFor(x => x.Member.Id).NotEmpty();
            RuleFor(x => x.Member.FullName).NotEmpty().WithMessage("Full Name is required.");
            RuleFor(x => x.Member.Email).EmailAddress().When(x => !string.IsNullOrEmpty(x.Member.Email));
            RuleFor(x => x.Member.StartDate).LessThan(x => x.Member.ExpirationDate).WithMessage("Start Date must be before Expiration Date.");
        }
    }
}
