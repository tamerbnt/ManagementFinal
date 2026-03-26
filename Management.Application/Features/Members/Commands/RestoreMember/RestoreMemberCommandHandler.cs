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

            // PUBLISH NOTIFICATION (Decoupled)
            _ = Task.Run(async () => 
            {
                try 
                {
                    var member = await _memberRepository.GetByIdAsync(request.Id);
                    if (member != null)
                    {
                        await _mediator.Publish(new Application.Notifications.FacilityActionCompletedNotification(
                            member.FacilityId,
                            "MemberRestore",
                            member.FullName,
                            $"Restored member {member.FullName}"));
                    }
                }
                catch (System.Exception ex)
                {
                    _logger?.LogWarning(ex, "Failed to publish restore notification for member {MemberId}", request.Id);
                }
            });

            return Result.Success();
        }
    }
}
