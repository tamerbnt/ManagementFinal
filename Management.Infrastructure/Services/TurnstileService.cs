using Management.Domain.DTOs;
using Management.Domain.Enums;
using Management.Domain.Interfaces;
using Management.Domain.Services;
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

        public async Task<List<TurnstileDto>> GetAllTurnstilesAsync()
        {
            var entities = await _turnstileRepository.GetAllAsync();

            // Map Entity -> DTO
            return entities.Select(t => new TurnstileDto
            {
                Id = t.Id,
                Name = t.Name,
                Status = t.Status,
                HardwareId = t.HardwareId,
                LastHeartbeat = t.LastHeartbeat
            }).ToList();
        }

        public async Task UpdateStatusAsync(Guid id, TurnstileStatus status)
        {
            // GetByIdAsync throws EntityNotFoundException if missing
            var turnstile = await _turnstileRepository.GetByIdAsync(id);

            if (turnstile.Status != status)
            {
                turnstile.Status = status;
                await _turnstileRepository.UpdateAsync(turnstile);
            }
        }

        public async Task ForceOpenAsync(Guid id)
        {
            var turnstile = await _turnstileRepository.GetByIdAsync(id);

            // Logic: Simulate a "Open" signal sent to hardware
            // We update the Heartbeat to show connectivity
            turnstile.LastHeartbeat = DateTime.UtcNow;

            // If it was in an Error state, we might auto-reset it to Operational
            if (turnstile.Status == TurnstileStatus.OutOfOrder || turnstile.Status == TurnstileStatus.Unknown)
            {
                turnstile.Status = TurnstileStatus.Operational;
            }

            await _turnstileRepository.UpdateAsync(turnstile);

            // In a real implementation with TCP/Serial hardware, 
            // the command would be sent here:
            // await _hardwareClient.SendSignalAsync(turnstile.HardwareId, "OPEN");
        }
    }
}