using Management.Domain.Enums;
using Management.Domain.Interfaces;
using Management.Domain.Models;
using Management.Domain.Primitives;
using MediatR;
using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Management.Application.Features.Turnstiles.Commands.LogAccessEvent
{
    public class LogAccessEventCommandHandler : IRequestHandler<LogAccessEventCommand, Result<Guid>>
    {
        private readonly IAccessEventRepository _accessRepo;
        private readonly IPublisher _publisher;

        private readonly Microsoft.Extensions.Logging.ILogger<LogAccessEventCommandHandler> _logger;

        public LogAccessEventCommandHandler(
            IAccessEventRepository accessRepo, 
            IPublisher publisher,
            Microsoft.Extensions.Logging.ILogger<LogAccessEventCommandHandler> logger)
        {
            _accessRepo = accessRepo;
            _publisher = publisher;
            _logger = logger;
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
                request.Direction,
                request.Reason);

            accessEvent.FacilityId = request.FacilityId;

            await _accessRepo.AddAsync(accessEvent);

            // PUBLISH NOTIFICATION: This is critical for the "People Inside" card to update instantly.
            // ActionType "Access" is handled by the Bridge to trigger a UI refresh.
            // PUBLISH NOTIFICATION: Decoupled to prevent UI failure from stalling check-in.
            _ = Task.Run(async () => 
            {
                try 
                {
                    await _publisher.Publish(new Application.Notifications.FacilityActionCompletedNotification(
                        request.FacilityId,
                        "Access",
                        "Member Check-In",
                        request.Reason));
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning(ex, "Failed to publish access notification for card {CardId}", request.CardId);
                }
            });

            return Result.Success(accessEvent.Id);
        }
    }
}
