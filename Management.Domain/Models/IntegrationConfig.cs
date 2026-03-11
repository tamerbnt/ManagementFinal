using System;
using Management.Domain.Primitives;

namespace Management.Domain.Models
{
    /// <summary>
    /// Configuration for 3rd party services (Stripe, Twilio, Hardware Controllers).
    /// </summary>
    public class IntegrationConfig : Entity, ITenantEntity, IFacilityEntity
    {
        public Guid TenantId { get; set; }
        public Guid FacilityId { get; set; }

        public string ProviderName { get; private set; } = string.Empty;
        public string ApiKey { get; private set; } = string.Empty; // Should look into encryption
        public string ApiUrl { get; private set; } = string.Empty;
        public bool IsEnabled { get; private set; }

        private IntegrationConfig(Guid id, string providerName, string apiKey, string apiUrl) : base(id)
        {
            ProviderName = providerName;
            ApiKey = apiKey;
            ApiUrl = apiUrl;
            IsEnabled = true;
        }

        private IntegrationConfig() { }

        public static Result<IntegrationConfig> Create(string provider, string key, string url)
        {
            return Result.Success(new IntegrationConfig(Guid.NewGuid(), provider, key, url));
        }
        public void UpdateDetails(string key, string url)
        {
            ApiKey = key;
            ApiUrl = url;
            UpdateTimestamp();
        }

        public void Enable() { IsEnabled = true; UpdateTimestamp(); }
        public void Disable() { IsEnabled = false; UpdateTimestamp(); }
    }
}
