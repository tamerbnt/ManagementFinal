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

        public DeleteMemberCommandHandler(IMemberRepository memberRepository)
        {
            _memberRepository = memberRepository;
        }

        public async Task<Result> Handle(DeleteMemberCommand request, CancellationToken cancellationToken)
        {
            var member = await _memberRepository.GetByIdAsync(request.Id);
            if (member == null)
            {
                return Result.Failure(new Error("Member.NotFound", $"Member with ID {request.Id} was not found."));
            }

            await _memberRepository.DeleteAsync(request.Id);


            return Result.Success();
        }
    }
}
