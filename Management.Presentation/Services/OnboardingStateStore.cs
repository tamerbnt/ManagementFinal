using System;

namespace Management.Presentation.Services
{
    public interface IOnboardingStateStore
    {
        string? ExpansionMessage { get; set; }
        Guid? TargetTenantId { get; set; }
        string? LicenseKey { get; set; }
        void Clear();
    }

    public class OnboardingStateStore : IOnboardingStateStore
    {
        public string? ExpansionMessage { get; set; }
        public Guid? TargetTenantId { get; set; }
        public string? LicenseKey { get; set; }

        public void Clear()
        {
            ExpansionMessage = null;
            TargetTenantId = null;
            LicenseKey = null;
        }
    }
}
