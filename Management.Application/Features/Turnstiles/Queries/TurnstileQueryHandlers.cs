using Management.Application.DTOs;
using Management.Domain.Interfaces;
using Management.Domain.Models;
using Management.Domain.Primitives;
using MediatR;
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

        public TurnstileQueryHandlers(ITurnstileRepository turnstileRepository, IAccessEventRepository accessRepository)
        {
            _turnstileRepository = turnstileRepository;
            _accessRepository = accessRepository;
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
            // Fix: Use Repository-level filtering to leverage indexes!
            IEnumerable<AccessEvent> events;
            
            if (request.FromDate.HasValue)
            {
                events = await _accessRepository.GetByDateRangeAsync(request.FacilityId, request.FromDate.Value, DateTime.MaxValue);
            }
            else
            {
                events = await _accessRepository.GetRecentEventsAsync(request.FacilityId, 100);
            }

            if (request.TurnstileId.HasValue)
            {
                events = events.Where(e => e.TurnstileId == request.TurnstileId.Value);
            }

            var dtos = events.Select(e => new AccessEventDto
            {
                Id = e.Id,
                Timestamp = e.Timestamp,
                TurnstileId = e.TurnstileId,
                CardId = e.CardId,
                IsAccessGranted = e.IsAccessGranted,
                AccessStatus = e.AccessStatus.ToString(),
                FailureReason = e.FailureReason
            }).ToList();

            return Result.Success(dtos);
        }

        public async Task<Result<int>> Handle(GetCurrentOccupancyQuery request, CancellationToken cancellationToken)
        {
            // Use optimized Repository method instead of fetching all events
            var count = await _accessRepository.GetCurrentOccupancyCountAsync(request.FacilityId);
            return Result.Success(count);
        }
    }
}
