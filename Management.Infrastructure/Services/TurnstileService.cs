using Management.Domain.DTOs;
using Management.Domain.Enums;
using Management.Domain.Interfaces;
using Management.Domain.Services;
using Management.Domain.Primitives;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Management.Infrastructure.Services
{
    public class TurnstileService : ITurnstileService
    {
        private readonly ITurnstileRepository _turnstileRepository;

        public TurnstileService(ITurnstileRepository turnstileRepository)
        {
            _turnstileRepository = turnstileRepository;
        }

        public async Task<Result<List<TurnstileDto>>> GetAllTurnstilesAsync()
        {
            var entities = await _turnstileRepository.GetAllAsync();

            var dtos = entities.Select(t => new TurnstileDto
            {
                Id = t.Id,
                Name = t.Name,
                Location = t.Location,
                IsLocked = t.IsLocked,
                Status = t.Status
            }).ToList();

            return Result.Success(dtos);
        }

        public async Task<Result> UpdateStatusAsync(Guid id, TurnstileStatus status)
        {
            var turnstile = await _turnstileRepository.GetByIdAsync(id);

            turnstile.UpdateStatus(status);
            await _turnstileRepository.UpdateAsync(turnstile);

            return Result.Success();
        }

        public async Task<Result> ForceOpenAsync(Guid id)
        {
            var turnstile = await _turnstileRepository.GetByIdAsync(id);

            turnstile.Unlock();
            await _turnstileRepository.UpdateAsync(turnstile);

            // In a real implementation:
            // await _hardwareClient.SendSignalAsync(turnstile.HardwareId, "OPEN");

            return Result.Success();
        }
    }
}