using FluentValidation;

namespace Management.Application.Features.Members.Commands.DeleteMember
{
    public class DeleteMemberCommandValidator : AbstractValidator<DeleteMemberCommand>
    {
        public DeleteMemberCommandValidator()
        {
            RuleFor(x => x.Id).NotEmpty().WithMessage("Member ID is required for deletion.");
        }
    }
}
