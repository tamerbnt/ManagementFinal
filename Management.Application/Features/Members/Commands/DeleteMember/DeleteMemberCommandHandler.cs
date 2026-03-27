using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
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
        private readonly Microsoft.Extensions.Logging.ILogger<DeleteMemberCommandHandler> _logger;

        public DeleteMemberCommandHandler(
            IMemberRepository memberRepository, 
            IMediator mediator,
            Microsoft.Extensions.Logging.ILogger<DeleteMemberCommandHandler> logger)
        {
            _memberRepository = memberRepository;
            _mediator = mediator;
            _logger = logger;
        }

        public async Task<Result> Handle(DeleteMemberCommand request, CancellationToken cancellationToken)
        {
            var member = await _memberRepository.GetByIdAsync(request.Id);
            if (member == null)
            {
                return Result.Failure(new Error("Member.NotFound", $"Member with ID {request.Id} was not found."));
            }

            await _memberRepository.DeleteAsync(request.Id);

            // NOTIFICATION REMOVED: User does not want delete history in Recent Activity.
            // If needed for audit in the future, this should be sent to a dedicated audit stream, not the dashboard.


            return Result.Success();
        }
    }
}
