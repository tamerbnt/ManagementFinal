using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Management.Domain.Interfaces;
using Management.Domain.Primitives;
using MediatR;

namespace Management.Application.Features.Members.Commands.RestoreMember
{
    public class RestoreMemberCommandHandler : IRequestHandler<RestoreMemberCommand, Result>
    {
        private readonly IMemberRepository _memberRepository;
        private readonly IMediator _mediator;
        private readonly Microsoft.Extensions.Logging.ILogger<RestoreMemberCommandHandler> _logger;

        public RestoreMemberCommandHandler(
            IMemberRepository memberRepository, 
            IMediator mediator,
            Microsoft.Extensions.Logging.ILogger<RestoreMemberCommandHandler> logger)
        {
            _memberRepository = memberRepository;
            _mediator = mediator;
            _logger = logger;
        }

        public async Task<Result> Handle(RestoreMemberCommand request, CancellationToken cancellationToken)
        {
            await _memberRepository.RestoreAsync(request.Id);

            // NOTIFICATION REMOVED: User does not want delete/restore history in Recent Activity.
            // If needed for audit in the future, this should be sent to a dedicated audit stream, not the dashboard.

            return Result.Success();
        }
    }
}
