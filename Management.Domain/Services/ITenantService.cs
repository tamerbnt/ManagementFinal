using System;

namespace Management.Domain.Services
{
    public interface ITenantService
    {
        Guid? GetTenantId();
        void SetTenantId(Guid tenantId);
        
        Guid? GetUserId();
        void SetUserId(Guid userId);
        
        string? GetRole();
        void SetRole(string role);
        
        string GetHardwareId();
        
        void Clear();

        /// <summary>
        /// Temporarily overrides the current tenant context for the current execution flow.
        /// </summary>
        IDisposable Impersonate(Guid tenantId);
    }
}
