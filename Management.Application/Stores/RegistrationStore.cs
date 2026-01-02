using System;
using Management.Application.DTOs;

namespace Management.Application.Stores
{
    /// <summary>
    /// Event Aggregator for Registration/Lead workflows.
    /// Used to synchronize the "Pending Leads" counters on the Dashboard and Sidebar
    /// when items are processed in the Registrations View.
    /// </summary>
    public class RegistrationStore
    {
        // Fired when a new lead is created (e.g. via API sync or manual add)
        public event Action<RegistrationDto>? RegistrationAdded;

        // Fired when lead details (Notes, Plan) are edited
        public event Action<RegistrationDto>? RegistrationUpdated;

        // Fired when a lead is Approved (Converted) or Declined (Removed).
        // The ID is passed so subscribers can remove it from their local lists.
        public event Action<Guid>? RegistrationProcessed;

        /// <summary>
        /// Broadcasts that a new registration has entered the system.
        /// </summary>
        public void TriggerRegistrationAdded(RegistrationDto registration)
        {
            RegistrationAdded?.Invoke(registration);
        }

        /// <summary>
        /// Broadcasts updates to an existing registration.
        /// </summary>
        public void TriggerRegistrationUpdated(RegistrationDto registration)
        {
            RegistrationUpdated?.Invoke(registration);
        }

        /// <summary>
        /// Broadcasts that a registration has been handled (removed from Pending state).
        /// </summary>
        public void TriggerRegistrationProcessed(Guid id)
        {
            RegistrationProcessed?.Invoke(id);
        }
    }
}