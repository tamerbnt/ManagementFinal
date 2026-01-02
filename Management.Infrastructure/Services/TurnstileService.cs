using Management.Application.DTOs;
using Management.Application.Services;
using Management.Domain.Enums;
using Management.Application.Services;
using Management.Domain.Interfaces;
using Management.Application.Services;
using Management.Domain.Services;
using Management.Application.Services;
using Management.Domain.Primitives;
using Management.Application.Services;
using System;
using Management.Application.Services;
using System.Collections.Generic;
using Management.Application.Services;
using System.Linq;
using Management.Application.Services;
using System.Threading.Tasks;
using Management.Application.Services;

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