using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Management.Domain.DTOs;
using Management.Domain.Interfaces;
using Management.Domain.Services;

namespace Management.Application.Services
{
    public class ReservationService : IReservationService
    {
        private readonly IReservationRepository _repository;

        public ReservationService(IReservationRepository repository)
        {
            _repository = repository;
        }

        public async Task<List<ReservationDto>> GetReservationsByRangeAsync(DateTime start, DateTime end)
        {
            var entities = await _repository.GetByDateRangeAsync(start, end);
            return entities.Select(e => new ReservationDto
            {
                Id = e.Id,
                ActivityName = e.ActivityName,
                InstructorName = e.InstructorName,
                Location = e.Location,
                StartTime = e.StartTime,
                EndTime = e.EndTime
            }).ToList();
        }

        public async Task<List<ReservationDto>> GetReservationsByMemberAsync(Guid memberId)
        {
            var entities = await _repository.GetByMemberIdAsync(memberId);
            // Same mapping...
            return entities.Select(e => new ReservationDto
            {
                Id = e.Id,
                ActivityName = e.ActivityName,
                StartTime = e.StartTime
            }).ToList();
        }

        public async Task CancelReservationAsync(Guid id)
        {
            // In V2, add check for cancellation window (e.g., 24h notice)
            await _repository.DeleteAsync(id);
        }
    }
}