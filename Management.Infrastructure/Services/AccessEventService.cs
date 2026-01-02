using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Management.Application.Features.Members.Queries.SearchMembers;
using Management.Application.Features.Turnstiles.Commands.LogAccessEvent;
using Management.Application.Features.Turnstiles.Queries;
using Management.Domain.DTOs;
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

        public AccessEventService(ISender sender)
        {
            _sender = sender;
        }

        public async Task<Result<List<AccessEventDto>>> GetRecentEventsAsync(Guid facilityId, int count = 50)
        {
            var result = await _sender.Send(new GetAccessEventsQuery());
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
            return await _sender.Send(new GetAccessEventsQuery(null, start));
        }

        public async Task<Result<int>> GetCurrentOccupancyAsync(Guid facilityId)
        {
            return await _sender.Send(new GetCurrentOccupancyQuery());
        }

        public async Task<Result> SimulateScanAsync(Guid facilityId, Guid? turnstileId = null)
        {
            return Result.Success();
        }

        public async Task<Result<AccessEventDto>> ProcessAccessRequestAsync(string cardId, Guid facilityId)
        {
            var searchResult = await _sender.Send(new SearchMembersQuery(new MemberSearchRequest(cardId, MemberFilterType.All)));
            
            MemberDto? member = null;
            if (searchResult.IsSuccess && searchResult.Value.Items.Any())
            {
                member = searchResult.Value.Items.FirstOrDefault(); 
            }

            bool granted = false;
            string status = "Denied";
            string reason = "Unknown Card";
            string memberName = "Unknown";
            string displayCardId = cardId;

            if (member != null)
            {
                memberName = member.FullName;
                displayCardId = member.CardId;

                if (member.Status == MemberStatus.Active && member.ExpirationDate > DateTime.UtcNow)
                {
                    granted = true;
                    status = "Granted";
                    reason = "Active Membership";
                }
                else
                {
                    status = "Denied";
                    reason = member.Status == MemberStatus.Active ? "Expired" : "Inactive Membership";
                }
            }

            var logCommand = new LogAccessEventCommand(
                TurnstileId: Guid.Empty,
                CardId: displayCardId,
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
                CardId = displayCardId,
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
