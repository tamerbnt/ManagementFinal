using Management.Domain.DTOs;
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
            var dtos = turnstiles.Select(t => new TurnstileDto
            {
                Id = t.Id,
                Name = t.Name,
                Location = t.Location,
                Status = t.Status,
                IsLocked = t.IsLocked
            }).ToList();

            return Result.Success(dtos);
        }

        public async Task<Result<List<AccessEventDto>>> Handle(GetAccessEventsQuery request, CancellationToken cancellationToken)
        {
            var events = await _accessRepository.GetAllAsync(); // TODO: Add filtering to Repo!
            
            // In-memory filtering for now as Repo generic might not support criteria.
            // Ideally add GetByDate/Turnstile to IAccessEventRepository.
            if (request.TurnstileId.HasValue)
            {
                events = events.Where(e => e.TurnstileId == request.TurnstileId.Value).ToList();
            }
            if (request.FromDate.HasValue)
            {
                events = events.Where(e => e.Timestamp >= request.FromDate.Value).ToList();
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
            var events = await _accessRepository.GetAllAsync();
            // Simple logic: Entries - Exits (assuming FacilityType or similar logic)
            // For now, return a placeholder or simple count of 'Granted' recently.
            // In a real app, this would query a summary table or use Entry/Exit sensor data.
            var count = events.Count(e => e.IsAccessGranted && e.Timestamp > DateTime.UtcNow.AddHours(-12));
            return Result.Success(count);
        }
    }
}
