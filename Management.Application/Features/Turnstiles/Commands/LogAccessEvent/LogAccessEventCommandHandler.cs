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
        private readonly IPublisher _publisher;

        public LogAccessEventCommandHandler(IAccessEventRepository accessRepo, IPublisher publisher)
        {
            _accessRepo = accessRepo;
            _publisher = publisher;
        }

        public async Task<Result<Guid>> Handle(LogAccessEventCommand request, CancellationToken cancellationToken)
        {
            if (!Enum.TryParse<AccessStatus>(request.Status, true, out var status))
            {
                status = AccessStatus.Denied;
            }

            var accessEvent = AccessEvent.Create(
                request.TurnstileId,
                request.CardId,
                request.TransactionId,
                request.Granted,
                status,
                request.Reason);

            accessEvent.FacilityId = request.FacilityId;

            await _accessRepo.AddAsync(accessEvent);

            // PUBLISH NOTIFICATION: This is critical for the "People Inside" card to update instantly.
            // ActionType "Access" is handled by the Bridge to trigger a UI refresh.
            await _publisher.Publish(new Application.Notifications.FacilityActionCompletedNotification(
                request.FacilityId,
                "Access",
                "Member Check-In",
                request.Reason), cancellationToken);

            return Result.Success(accessEvent.Id);
        }
    }
}
