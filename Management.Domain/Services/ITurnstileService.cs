using Management.Domain.DTOs;
using Management.Domain.Enums;
using Management.Domain.Primitives;
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
        Task<Result<List<TurnstileDto>>> GetAllTurnstilesAsync();

        /// <summary>
        /// Manually updates the operational mode of a turnstile (e.g., Lockdown, Maintenance).
        /// </summary>
        Task<Result> UpdateStatusAsync(Guid id, TurnstileStatus status);

        /// <summary>
        /// Sends a "One-Time Open" command to the hardware (for manual staff override).
        /// </summary>
        Task<Result> ForceOpenAsync(Guid id);
    }
}