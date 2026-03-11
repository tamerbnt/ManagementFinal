using System;

namespace Management.Presentation.Services
{
    public interface IOnboardingStateStore
    {
        string? ExpansionMessage { get; set; }
        Guid? TargetTenantId { get; set; }
        string? LicenseKey { get; set; }
        Guid? LicenseId { get; set; }
        string? BusinessName { get; set; }
        string? AdminFullName { get; set; }
        string? AdminEmail { get; set; }
        void Clear();
    }

    public class OnboardingStateStore : IOnboardingStateStore, Management.Domain.Interfaces.IStateResettable
    {
        public void ResetState()
        {
            Clear();
        }
        public string? ExpansionMessage { get; set; }
        public Guid? TargetTenantId { get; set; }
        public string? LicenseKey { get; set; }
        public Guid? LicenseId { get; set; }
        public string? BusinessName { get; set; }
        public string? AdminFullName { get; set; }
        public string? AdminEmail { get; set; }

        public void Clear()
        {
            ExpansionMessage = null;
            TargetTenantId = null;
            LicenseKey = null;
            LicenseId = null;
            BusinessName = null;
            AdminFullName = null;
            AdminEmail = null;
        }
    }
}
