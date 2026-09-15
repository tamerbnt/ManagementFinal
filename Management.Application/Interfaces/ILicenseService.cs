using System;
using System.Threading.Tasks;
using Management.Application.DTOs;

namespace Management.Application.Interfaces
{
    public interface ILicenseService
    {
        // Legacy Signature (Preserved for backward compatibility)
        Task<LicenseCheckResult> ValidateLicenseAsync(string key, string hardwareId);

        // Phase 2 Additions: Device Handshake & Subscription Gate
        Task<LicenseCheckResult> CheckDeviceRegistrationAsync(string hardwareId);
        Task<LicenseCheckResult> CheckSubscriptionStatusAsync(string hardwareId);
        Task<LicenseCheckResult> RedeemVoucherAsync(Guid accountId, string voucherCode);
        Task<LicenseCheckResult> RegisterDeviceAsync(Guid accountId, Guid branchId, string hardwareId, string label);
        Task<bool> RevokeDeviceAsync(Guid deviceId, Guid accountId);
    }
}
