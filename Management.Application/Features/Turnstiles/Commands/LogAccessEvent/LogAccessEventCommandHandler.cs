using Management.Domain.Enums;
using Management.Domain.Interfaces;
using Management.Domain.Models;
using Management.Domain.Primitives;
using MediatR;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Management.Application.Features.Turnstiles.Commands.LogAccessEvent
{
    public class LogAccessEventCommandHandler : IRequestHandler<LogAccessEventCommand, Result<Guid>>
    {
        private readonly IAccessEventRepository _accessRepo;

        public LogAccessEventCommandHandler(IAccessEventRepository accessRepo)
        {
            _accessRepo = accessRepo;
        }

        public async Task<Result<Guid>> Handle(LogAccessEventCommand request, CancellationToken cancellationToken)
        {
            if (!Enum.TryParse<AccessStatus>(request.Status, true, out var status))
            {
                status = AccessStatus.Denied; // Default or Error?
            }

            var accessEvent = AccessEvent.Create(
                request.TurnstileId,
                request.CardId,
                request.Granted,
                status,
                request.Reason);

            await _accessRepo.AddAsync(accessEvent);

            return Result.Success(accessEvent.Id);
        }
    }
}
