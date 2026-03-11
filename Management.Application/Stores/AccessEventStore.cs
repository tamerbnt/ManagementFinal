using System;
using Management.Application.DTOs;

using Management.Domain.Interfaces;

namespace Management.Application.Stores
{
    /// <summary>
    /// High-Frequency Event Bus for Access Logs.
    /// Acts as the bridge between the Hardware/Service layer and the UI.
    /// 
    /// Architecture Note: This store is STATELESS. It does not hold a list of logs in memory
    /// to prevent memory leaks over long uptimes. It blindly forwards the DTO to subscribers
    /// (Dashboard, AccessControlView) who manage their own buffers.
    /// </summary>
    public class AccessEventStore : IStateResettable
    {
        public void ResetState()
        {
            // Stateless bus, nothing to clear
        }
        /// <summary>
        /// Fired immediately when a scan is processed (Granted or Denied).
        /// Subscribers should marshal this to the UI thread if updating Collections.
        /// </summary>
        public event Action<AccessEventDto>? AccessEventLogged;

        /// <summary>
        /// Broadcasts a new access event to all active screens.
        /// </summary>
        /// <param name="accessEvent">The DTO containing Member Name, Status, Time, etc.</param>
        public void TriggerAccessEvent(AccessEventDto accessEvent)
        {
            AccessEventLogged?.Invoke(accessEvent);
        }
    }
}
