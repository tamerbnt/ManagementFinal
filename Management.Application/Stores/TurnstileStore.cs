using Management.Application.DTOs;
using System;

namespace Management.Application.Stores
{
    /// <summary>
    /// Event Aggregator for Hardware State synchronization.
    /// Bridges the gap between low-level hardware services (TurnstileService) and the UI (AccessControlViewModel).
    /// 
    /// Architecture Note: This store is STATELESS. It does not cache the list of turnstiles.
    /// It relies on the ViewModel to hold the collection and update specific items by ID
    /// when an event is received.
    /// </summary>
    public class TurnstileStore
    {
        /// <summary>
        /// Fired when a hardware unit reports a status change (e.g. Locked, Error) 
        /// or a heartbeat update.
        /// </summary>
        public event Action<TurnstileDto>? TurnstileUpdated;

        /// <summary>
        /// Broadcasts a hardware state change to the application.
        /// </summary>
        /// <param name="turnstile">The updated DTO containing the new Status and LastHeartbeat.</param>
        public void TriggerTurnstileUpdated(TurnstileDto turnstile)
        {
            TurnstileUpdated?.Invoke(turnstile);
        }
    }
}