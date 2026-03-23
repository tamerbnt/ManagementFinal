using System.Threading;
using System.Threading.Tasks;
using Management.Application.Features.Members.Commands.DeleteMember;
using Management.Domain.Interfaces;
using Management.Domain.Primitives;
using MediatR;

namespace Management.Application.Features.Members.Commands.DeleteMember
{
    public class DeleteMemberCommandHandler : IRequestHandler<DeleteMemberCommand, Result>
    {
        private readonly IMemberRepository _memberRepository;
        private readonly IMediator _mediator;

        public DeleteMemberCommandHandler(IMemberRepository memberRepository, IMediator mediator)
        {
            _memberRepository = memberRepository;
            _mediator = mediator;
        }

        public async Task<Result> Handle(DeleteMemberCommand request, CancellationToken cancellationToken)
        {
            var member = await _memberRepository.GetByIdAsync(request.Id);
            if (member == null)
            {
                return Result.Failure(new Error("Member.NotFound", $"Member with ID {request.Id} was not found."));
            }

            await _memberRepository.DeleteAsync(request.Id);

            // PUBLISH NOTIFICATION (Note: No undo for delete in this version as per standard pattern)
            await _mediator.Publish(new Application.Notifications.FacilityActionCompletedNotification(
                member.FacilityId,
                "MemberDelete",
                member.FullName,
                $"Deleted member {member.FullName}"), cancellationToken);


            return Result.Success();
        }
    }
}
