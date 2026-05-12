using System;
using System.Threading;
using Management.Domain.Services;
using Management.Application.Interfaces;

namespace Management.Infrastructure.Services
{
    public class TenantService : ITenantService
    {
        private readonly IHardwareService _hardwareService;
        
        // Global State (Singleton fields)
        private Guid? _globalTenantId;
        private Guid? _globalUserId;
        private string? _globalRole;

        // Ambient Overrides (Isolated to the current execution flow)
        private static readonly AsyncLocal<Guid?> _overrideTenantId = new();

        public TenantService(IHardwareService hardwareService)
        {
            _hardwareService = hardwareService;
        }

        // Return override if present, otherwise fall back to global state
        public Guid? GetTenantId() => _overrideTenantId.Value ?? _globalTenantId;
        public void SetTenantId(Guid tenantId) => _globalTenantId = tenantId;

        public Guid? GetUserId() => _globalUserId;
        public void SetUserId(Guid userId) => _globalUserId = userId;

        public string? GetRole() => _globalRole;
        public void SetRole(string role) => _globalRole = role;

        public string GetHardwareId() => _hardwareService.GetHardwareId();

        public void Clear()
        {
            _globalTenantId = null;
            _globalUserId = null;
            _globalRole = null;
            _overrideTenantId.Value = null;
        }

        public IDisposable Impersonate(Guid tenantId)
        {
            var previous = _overrideTenantId.Value;
            _overrideTenantId.Value = tenantId;
            return new ContextRestorer(() => _overrideTenantId.Value = previous);
        }

        private class ContextRestorer : IDisposable
        {
            private readonly Action _restoreAction;
            public ContextRestorer(Action restoreAction) => _restoreAction = restoreAction;
            public void Dispose() => _restoreAction();
        }
    }
}
