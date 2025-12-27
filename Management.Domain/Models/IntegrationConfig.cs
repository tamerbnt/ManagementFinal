using System;

namespace Management.Domain.Models
{
    /// <summary>
    /// Configuration for 3rd party services (Stripe, Twilio, Hardware Controllers).
    /// </summary>
    public class IntegrationConfig : Entity
    {
        public string ProviderName { get; set; } // "Stripe"
        public bool IsConnected { get; set; }
        public string ApiKey { get; set; } // Encrypted in DB
        public string IconKey { get; set; } // Resource Key for UI
        public string Description { get; set; }

        // Provider specific config
        public string AdditionalConfigJson { get; set; }
    }
}