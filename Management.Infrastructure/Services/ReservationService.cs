using Management.Application.DTOs;
using Management.Application.Services;
using Management.Domain.Interfaces;
using Management.Domain.Models;
using Management.Domain.Primitives;
using Management.Domain.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Management.Infrastructure.Services
{
    public class ReservationService : IReservationService
    {
        private readonly IRepository<Reservation> _reservationRepository;

        public ReservationService(IRepository<Reservation> reservationRepository)
        {
            _reservationRepository = reservationRepository;
        }

        public async Task<Result<List<ReservationDto>>> GetReservationsByRangeAsync(DateTime start, DateTime end)
        {
            var entities = await _reservationRepository.GetAllAsync();
            var filtered = entities.Where(r => r.StartTime >= start && r.StartTime <= end).ToList();
            
            var dtos = filtered.Select(r => new ReservationDto(
                r.Id,
                r.ResourceType ?? "Unknown Activity",
                "TBD", // InstructorName - not in model
                "TBD", // Location - not in model
                r.StartTime,
                r.EndTime
            )).ToList();

            return Result.Success(dtos);
        }

        public async Task<Result<List<ReservationDto>>> GetReservationsByMemberAsync(Guid memberId)
        {
            var entities = await _reservationRepository.GetAllAsync();
            var filtered = entities.Where(r => r.MemberId == memberId && r.StartTime >= DateTime.UtcNow).ToList();
            
            var dtos = filtered.Select(r => new ReservationDto(
                r.Id,
                r.ResourceType ?? "Unknown Activity",
                "TBD", // InstructorName - not in model
                "TBD", // Location - not in model
                r.StartTime,
                r.EndTime
            )).ToList();

            return Result.Success(dtos);
        }

        public async Task<Result> CancelReservationAsync(Guid id)
        {
            var entity = await _reservationRepository.GetByIdAsync(id);
            if (entity == null)
            {
                return Result.Failure(new Error("Reservation.NotFound", $"Reservation {id} not found."));
            }

            entity.Cancel();
            await _reservationRepository.UpdateAsync(entity);
            return Result.Success();
        }
    }
}
