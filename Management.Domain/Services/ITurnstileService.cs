using Management.Domain.DTOs;
using Management.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Management.Domain.Services
{
    public interface ITurnstileService
    {
        /// <summary>
        /// Retrieves the current status of all registered hardware units.
        /// </summary>
        Task<List<TurnstileDto>> GetAllTurnstilesAsync();

        /// <summary>
        /// Manually updates the operational mode of a turnstile (e.g., Lockdown, Maintenance).
        /// </summary>
        /// <exception cref="Management.Domain.Exceptions.EntityNotFoundException">Thrown if ID is invalid.</exception>
        Task UpdateStatusAsync(Guid id, TurnstileStatus status);

        /// <summary>
        /// Sends a "One-Time Open" command to the hardware (for manual staff override).
        /// </summary>
        /// <exception cref="Management.Domain.Exceptions.BusinessRuleViolationException">Thrown if hardware is offline.</exception>
        Task ForceOpenAsync(Guid id);
    }
}