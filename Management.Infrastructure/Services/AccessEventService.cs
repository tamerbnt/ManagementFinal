using System.Collections.Generic;
using Management.Application.Services;
using System.Linq;
using System.Threading.Tasks;
using Management.Application.Features.Members.Queries.SearchMembers;
using Management.Application.Features.Turnstiles.Commands.LogAccessEvent;
using Management.Application.Features.Turnstiles.Queries;
using Management.Application.DTOs;
using Management.Domain.Enums;
using Management.Domain.Primitives;
using Management.Domain.Services;
using MediatR;
using System;

namespace Management.Infrastructure.Services
{
    public class AccessEventService : IAccessEventService
    {
        private readonly ISender _sender;
        private readonly IAccessControlService _accessControl;

        public AccessEventService(
            ISender sender, 
            IAccessControlService accessControl)
        {
            _sender = sender;
            _accessControl = accessControl;
        }

        public async Task<Result<List<AccessEventDto>>> GetRecentEventsAsync(Guid facilityId, int count = 50)
        {
            var result = await _sender.Send(new GetAccessEventsQuery(facilityId));
            if (result.IsFailure) return result;
            
            var list = result.Value;
            if (list.Count > count)
            {
                list = list.GetRange(0, count);
            }
            return Result.Success(list);
        }

        public async Task<Result<List<AccessEventDto>>> GetEventsByRangeAsync(Guid facilityId, DateTime start, DateTime end)
        {
            return await _sender.Send(new GetAccessEventsQuery(facilityId, null, start));
        }

        public async Task<Result<int>> GetCurrentOccupancyAsync(Guid facilityId)
        {
            return await _sender.Send(new GetCurrentOccupancyQuery(facilityId));
        }

        public async Task<Result> SimulateScanAsync(Guid facilityId, Guid? turnstileId = null)
        {
            // Simulation logic is handled by the hardware mock usually, 
            // but we can trigger a manual processing here for testing.
            return Result.Success();
        }

        /// <summary>
        /// Processes a scan event from the hardware. Validates the member and logs the result.
        /// </summary>
        public async Task<Result<AccessEventDto>> ProcessAccessRequestAsync(string cardId, Guid facilityId, string? transactionId = null)
        {
            // 1. Run the access validation
            var validationResult = await _accessControl.ProcessScanAsync(cardId);
            
            bool granted = validationResult.Status == AccessResult.Granted || validationResult.Status == AccessResult.Warning;
            string status = validationResult.Status.ToString();
            string reason = validationResult.Message;
            string memberName = validationResult.Member?.FullName ?? "Unknown";

            var logCommand = new LogAccessEventCommand(
                FacilityId: facilityId,
                TurnstileId: Guid.Empty, 
                CardId: cardId,
                TransactionId: transactionId ?? string.Empty,
                Granted: granted,
                Status: status,
                Reason: reason
            );

            var logResult = await _sender.Send(logCommand);
            
            if (logResult.IsFailure)
            {
                return Result.Failure<AccessEventDto>(logResult.Error);
            }

            var dto = new AccessEventDto
            {
                Id = logResult.Value,
                MemberName = memberName,
                CardId = cardId,
                AccessStatus = status,
                IsAccessGranted = granted,
                FailureReason = reason,
                Timestamp = DateTime.UtcNow,
                FacilityName = "Reception"
            };

            return Result.Success(dto);
        }
    }
}
