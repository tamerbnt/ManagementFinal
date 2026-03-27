using Management.Application.DTOs;
using Management.Domain.Interfaces;
using Management.Domain.Models;
using Management.Domain.Primitives;
using MediatR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Management.Application.Features.Turnstiles.Queries
{
    public class TurnstileQueryHandlers : 
        IRequestHandler<GetTurnstilesQuery, Result<List<TurnstileDto>>>,
        IRequestHandler<GetAccessEventsQuery, Result<List<AccessEventDto>>>,
        IRequestHandler<GetCurrentOccupancyQuery, Result<int>>
    {
        private readonly ITurnstileRepository _turnstileRepository;
        private readonly IAccessEventRepository _accessRepository;
        private readonly IMemberRepository _memberRepository;

        public TurnstileQueryHandlers(
            ITurnstileRepository turnstileRepository, 
            IAccessEventRepository accessRepository,
            IMemberRepository memberRepository)
        {
            _turnstileRepository = turnstileRepository;
            _accessRepository = accessRepository;
            _memberRepository = memberRepository;
        }

        public async Task<Result<List<TurnstileDto>>> Handle(GetTurnstilesQuery request, CancellationToken cancellationToken)
        {
            var turnstiles = await _turnstileRepository.GetAllAsync();
            var dtos = turnstiles
                .Where(t => t.FacilityId == request.FacilityId)
                .Select(t => new TurnstileDto
                {
                    Id = t.Id,
                    Name = t.Name,
                    Location = t.Location ?? string.Empty,
                    HardwareId = t.HardwareId,
                    IsLocked = t.IsLocked,
                    Status = t.Status,
                    LastHeartbeat = t.UpdatedAt ?? t.CreatedAt
                }).ToList();

            return Result.Success(dtos);
        }

        public async Task<Result<List<AccessEventDto>>> Handle(GetAccessEventsQuery request, CancellationToken cancellationToken)
        {
            IEnumerable<AccessEvent> events;
            
            if (request.FromDate.HasValue)
            {
                events = await _accessRepository.GetByDateRangeAsync(request.FacilityId, request.FromDate.Value, DateTime.MaxValue);
            }
            else
            {
                events = await _accessRepository.GetRecentEventsAsync(request.FacilityId, 100);
            }

            // Resolve member names and filter out events from deleted members
            var cardIds = events.Select(e => e.CardId).Distinct().ToList();
            var memberMap = new Dictionary<string, Member>();

            if (cardIds.Any())
            {
                foreach (var cardId in cardIds)
                {
                    if (string.IsNullOrEmpty(cardId) || cardId == "WALK-IN") continue;
                    
                    var member = await _memberRepository.GetByCardIdAsync(cardId, request.FacilityId);
                    if (member != null) memberMap[cardId] = member;
                }
            }

            var dtos = events
                .Where(e => e.CardId == "WALK-IN" || memberMap.ContainsKey(e.CardId)) // Exclude if member was deleted
                .Select(e => new AccessEventDto
                {
                    Id = e.Id,
                    Timestamp = e.Timestamp,
                    TurnstileId = e.TurnstileId,
                    CardId = e.CardId,
                    MemberName = memberMap.TryGetValue(e.CardId, out var m) ? m.FullName : (e.CardId == "WALK-IN" ? "Walk-In Guest" : "Unknown"),
                    IsAccessGranted = e.IsAccessGranted,
                    AccessStatus = e.AccessStatus.ToString(),
                    FailureReason = e.FailureReason
                }).ToList();

            return Result.Success(dtos);
        }

        public async Task<Result<int>> Handle(GetCurrentOccupancyQuery request, CancellationToken cancellationToken)
        {
            var count = await _accessRepository.GetCurrentOccupancyCountAsync(request.FacilityId);
            return Result.Success(count);
        }
    }
}
