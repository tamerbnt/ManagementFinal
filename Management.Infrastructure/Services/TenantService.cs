using System;
using Management.Domain.Services;
using Management.Application.Interfaces;

namespace Management.Infrastructure.Services
{
    public class TenantService : ITenantService
    {
        private readonly IHardwareService _hardwareService;
        private Guid? _currentTenantId;
        private Guid? _currentUserId;
        private string? _currentRole;

        public TenantService(IHardwareService hardwareService)
        {
            _hardwareService = hardwareService;
        }

        public Guid? GetTenantId() => _currentTenantId;
        public void SetTenantId(Guid tenantId) => _currentTenantId = tenantId;

        public Guid? GetUserId() => _currentUserId;
        public void SetUserId(Guid userId) => _currentUserId = userId;

        public string? GetRole() => _currentRole;
        public void SetRole(string role) => _currentRole = role;

        public string GetHardwareId() => _hardwareService.GetHardwareId();

        public void Clear()
        {
            _currentTenantId = null;
            _currentUserId = null;
            _currentRole = null;
        }
    }
}
