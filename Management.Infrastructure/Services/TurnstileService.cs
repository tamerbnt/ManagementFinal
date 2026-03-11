using Management.Application.DTOs;
using Management.Application.Services;
using Management.Domain.Enums;
using Management.Domain.Interfaces;
using Management.Domain.Services;
using Management.Domain.Primitives;
using Management.Infrastructure.Hardware;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Management.Infrastructure.Services
{
    public class TurnstileService : ITurnstileService
    {
        private readonly ITurnstileRepository _turnstileRepository;
        private readonly IHardwareTurnstileService _hardwareTurnstile;

        public TurnstileService(
            ITurnstileRepository turnstileRepository,
            IHardwareTurnstileService hardwareTurnstile)
        {
            _turnstileRepository = turnstileRepository;
            _hardwareTurnstile = hardwareTurnstile;
        }

        public async Task<Result<List<TurnstileDto>>> GetAllTurnstilesAsync(Guid facilityId)
        {
            var entities = await _turnstileRepository.GetAllAsync();

            var dtos = entities
                .Where(t => t.FacilityId == facilityId)
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

        public async Task<Result> UpdateStatusAsync(Guid id, Guid facilityId, TurnstileStatus status)
        {
            var turnstile = await _turnstileRepository.GetByIdAsync(id, facilityId);
            if (turnstile == null) return Result.Failure(new Error("Turnstile.NotFound", "Turnstile not found"));

            turnstile.UpdateStatus(status);
            await _turnstileRepository.UpdateAsync(turnstile);

            return Result.Success();
        }

        public async Task<Result> ForceOpenAsync(Guid id, Guid facilityId)
        {
            var turnstile = await _turnstileRepository.GetByIdAsync(id, facilityId);
            if (turnstile == null)
            {
                return Result.Failure(new Error("Turnstile.NotFound", "Turnstile not found"));
            }

            // 1. Send signal to physical hardware
            await _hardwareTurnstile.OpenGateAsync();

            // 2. Track state in database (optional, hardware usually auto-locks)
            turnstile.Unlock();
            await _turnstileRepository.UpdateAsync(turnstile);

            return Result.Success();
        }
    }
}
