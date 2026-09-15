using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Supabase;
using Management.Application.Interfaces;
using Management.Application.DTOs;
using Management.Domain.Exceptions;
using System.Text.Json;

namespace Management.Infrastructure.Services
{
    public class LicenseService : ILicenseService
    {
        private readonly Client _supabase;
        private readonly ILogger<LicenseService> _logger;
        private readonly IConfigurationService? _configService;

        public LicenseService(Client supabase, ILogger<LicenseService> logger, IConfigurationService? configService = null)
        {
            _supabase = supabase;
            _logger = logger;
            _configService = configService;
        }

        public async Task<LicenseCheckResult> ValidateLicenseAsync(string licenseKey, string hardwareId)
        {
            if (string.IsNullOrWhiteSpace(licenseKey))
            {
                return LicenseCheckResult.Failure("License key cannot be empty.");
            }

            // 1. Sanitization
            var cleanKey = licenseKey.Trim().ToUpper();
            
            _logger.LogInformation("[LICENSE_AUDIT] Attempting to verify license key: {Key} for Hardware: {HardwareId}", cleanKey, hardwareId);

            try
            {
                var parameters = new Dictionary<string, object>
                {
                    // The keys MUST match the SQL function parameter names exactly
                    { "p_lookup_key", cleanKey },
                    { "p_hardware_id", hardwareId },
                    { "p_label", Environment.MachineName } // Added to disambiguate RPC
                };

                // Log the exact parameters being sent
                System.Diagnostics.Debug.WriteLine($"[LICENSE] Verification started for key {cleanKey} {DateTime.Now:HH:mm:ss.fff}");
                var sw = System.Diagnostics.Stopwatch.StartNew();
                _logger.LogInformation("[LICENSE_AUDIT] RPC Parameters: p_lookup_key={LicenseKey}, p_hardware_id={HardwareId}", cleanKey, hardwareId);

                // 2. Call the Postgres Function defined in SQL
                var response = await _supabase.Rpc("verify_license_key", parameters);
                
                sw.Stop();
                System.Diagnostics.Debug.WriteLine($"[LICENSE] Verification finished. Duration={sw.ElapsedMilliseconds}ms");
                
                // 3. Log raw response for debugging
                _logger.LogInformation("[LICENSE_AUDIT] Supabase RPC Raw Response: {Content}", response.Content ?? "<NULL>");
                _logger.LogInformation("[LICENSE_AUDIT] Response Status Code: {StatusCode}", response.ResponseMessage?.StatusCode);



                // 4. Parse Response
                // Supabase RPC returns a single JSON object (not an array)
                // Expected structure: { "valid": true/false, "message": "...", "license_id": "...", "tenant_id": null }
                using var doc = JsonDocument.Parse(response.Content ?? "{}");
                JsonElement root = doc.RootElement;
                
                _logger.LogInformation("[LICENSE_AUDIT] Root ValueKind: {ValueKind}", root.ValueKind);
                
                bool isValid = false;
                if (root.TryGetProperty("valid", out var validProp))
                {
                    isValid = validProp.GetBoolean();
                    _logger.LogInformation("[LICENSE_AUDIT] Parsed 'valid' property: {IsValid}", isValid);
                }
                else
                {
                    _logger.LogWarning("[LICENSE_AUDIT] 'valid' property NOT FOUND in response. Root ValueKind: {ValueKind}", root.ValueKind);
                }

                string message = string.Empty;
                if (root.TryGetProperty("message", out var msgProp))
                {
                    message = msgProp.GetString() ?? string.Empty;
                    _logger.LogInformation("[LICENSE_AUDIT] Parsed 'message' property: {Message}", message);
                }
                else
                {
                    _logger.LogWarning("[LICENSE_AUDIT] 'message' property NOT FOUND in response");
                }

                if (isValid)
                {
                    _logger.LogInformation("[LICENSE_AUDIT] ✓ License validation SUCCESSFUL for key: {Key}", cleanKey);
                    return LicenseCheckResult.Success();
                }
                else
                {
                    _logger.LogWarning("[LICENSE_AUDIT] ✗ License validation FAILED. Reason: {Message}", message);
                    _logger.LogWarning("[LICENSE_AUDIT] Throwing LicenseException with message: {ExceptionMessage}", message ?? "Invalid license key.");
                    throw new LicenseException(message ?? "Invalid license key.");
                }
            }
            catch (LicenseException)
            {
                // Re-throw license exceptions to be handled by the UI
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Critical error during license verification RPC.");
                throw new LicenseException("System error during license validation. Please try again later.", ex);
            }
        }

        // --- Phase 2 Stored RPC Implementations ---

        public async Task<LicenseCheckResult> CheckDeviceRegistrationAsync(string hardwareId)
        {
            if (string.IsNullOrWhiteSpace(hardwareId))
                return new LicenseCheckResult { IsValid = false, Status = "unregistered", FailureReason = "Hardware ID is required." };

            try
            {
                _logger.LogInformation("[DEVICE_AUDIT] Checking device registration for Hardware: {HardwareId}", hardwareId);
                var parameters = new Dictionary<string, object>
                {
                    { "p_hardware_id", hardwareId }
                };

                var response = await _supabase.Rpc("check_device_registration", parameters);
                if (response == null || string.IsNullOrWhiteSpace(response.Content) || response.Content == "null")
                {
                    return new LicenseCheckResult { IsValid = false, Status = "unregistered" };
                }

                using var doc = JsonDocument.Parse(response.Content);
                var root = doc.RootElement;

                bool isRegistered = root.TryGetProperty("is_registered", out var regProp) && regProp.GetBoolean();
                if (!isRegistered)
                {
                    return new LicenseCheckResult { IsValid = false, Status = "unregistered" };
                }

                bool isActive = !root.TryGetProperty("is_active", out var actProp) || actProp.GetBoolean();
                bool accountIsActive = !root.TryGetProperty("account_is_active", out var accProp) || accProp.GetBoolean();

                Guid? deviceId = root.TryGetProperty("device_id", out var devProp) && Guid.TryParse(devProp.GetString(), out var dId) ? dId : null;
                Guid? accountId = root.TryGetProperty("account_id", out var accIdProp) && Guid.TryParse(accIdProp.GetString(), out var aId) ? aId : null;
                Guid? branchId = root.TryGetProperty("branch_id", out var brProp) && Guid.TryParse(brProp.GetString(), out var bId) ? bId : null;
                string label = root.TryGetProperty("label", out var lblProp) ? (lblProp.GetString() ?? string.Empty) : string.Empty;

                bool isValid = isActive && accountIsActive;
                return new LicenseCheckResult
                {
                    IsValid = isValid,
                    Status = isValid ? "registered" : "inactive",
                    DeviceId = deviceId,
                    AccountId = accountId,
                    BranchId = branchId,
                    FacilityName = label,
                    FailureReason = !isValid ? (accountIsActive ? "Device has been deactivated." : "Account has been deactivated.") : null
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[DEVICE_AUDIT] Exception checking device registration for Hardware: {HardwareId}", hardwareId);
                return new LicenseCheckResult { IsValid = false, Status = "error", FailureReason = ex.Message };
            }
        }

        public async Task<LicenseCheckResult> CheckSubscriptionStatusAsync(string hardwareId)
        {
            if (string.IsNullOrWhiteSpace(hardwareId))
                return LicenseCheckResult.Failure("Hardware ID is required.");

            try
            {
                _logger.LogInformation("[SUB_AUDIT] Checking subscription status for Hardware: {HardwareId}", hardwareId);
                var parameters = new Dictionary<string, object>
                {
                    { "p_hardware_id", hardwareId }
                };

                var response = await _supabase.Rpc("check_subscription_status", parameters);
                if (response != null && !string.IsNullOrWhiteSpace(response.Content) && response.Content != "null")
                {
                    using var doc = JsonDocument.Parse(response.Content);
                    var root = doc.RootElement;

                    string status = root.TryGetProperty("status", out var stProp) ? (stProp.GetString() ?? "unknown") : "unknown";
                    bool isLifetime = root.TryGetProperty("is_lifetime", out var lifeProp) && lifeProp.GetBoolean();
                    string planName = root.TryGetProperty("plan_name", out var pnProp) ? (pnProp.GetString() ?? string.Empty) : string.Empty;
                    int tierRank = root.TryGetProperty("tier_rank", out var trProp) && trProp.TryGetInt32(out var tr) ? tr : 0;
                    int? daysRemaining = root.TryGetProperty("days_remaining", out var drProp) && drProp.ValueKind == JsonValueKind.Number && drProp.TryGetInt32(out var dr) ? dr : null;
                    bool crossBranch = root.TryGetProperty("cross_branch_reports", out var cbProp) && cbProp.GetBoolean();
                    bool sharedMembers = root.TryGetProperty("shared_members", out var smProp) && smProp.GetBoolean();

                    if (status == "active" || status == "trialing" || status == "grace_period")
                    {
                        if (_configService != null)
                        {
                            try
                            {
                                var existing = await _configService.LoadConfigAsync<Management.Domain.Models.LicenseLease>("license.lease");
                                var lease = new Management.Domain.Models.LicenseLease
                                {
                                    HardwareId = hardwareId,
                                    AccountId = existing?.AccountId,
                                    BranchId = existing?.BranchId,
                                    IsLifetime = isLifetime,
                                    PlanRank = tierRank,
                                    PlanName = planName,
                                    DaysRemaining = daysRemaining ?? 0,
                                    ExpiryDate = isLifetime ? DateTime.UtcNow.AddYears(100) : (daysRemaining.HasValue ? DateTime.UtcNow.AddDays(daysRemaining.Value) : DateTime.UtcNow.AddDays(14)),
                                    Signature = "SIGNED-" + hardwareId
                                };
                                await _configService.SaveConfigAsync(lease, "license.lease");
                            }
                            catch (Exception leaseEx)
                            {
                                _logger.LogWarning(leaseEx, "[SUB_AUDIT] Failed to persist offline lease.");
                            }
                        }

                        return new LicenseCheckResult
                        {
                            IsValid = true,
                            Status = status,
                            IsLifetime = isLifetime,
                            PlanName = planName,
                            PlanRank = tierRank,
                            DaysRemaining = daysRemaining ?? 0,
                            CrossBranchReports = crossBranch,
                            SharedMembers = sharedMembers
                        };
                    }
                    else if (status == "expired")
                    {
                        return new LicenseCheckResult
                        {
                            IsValid = false,
                            Status = "expired",
                            PlanName = planName,
                            PlanRank = tierRank,
                            DaysRemaining = 0,
                            FailureReason = "Subscription or trial has expired."
                        };
                    }
                    else
                    {
                        return new LicenseCheckResult
                        {
                            IsValid = false,
                            Status = status,
                            FailureReason = $"Subscription status: {status}"
                        };
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[SUB_AUDIT] Cloud check failed for Hardware {HardwareId}. Falling back to offline lease.", hardwareId);
            }

            // Offline lease fallback
            if (_configService != null)
            {
                try
                {
                    var lease = await _configService.LoadConfigAsync<Management.Domain.Models.LicenseLease>("license.lease");
                    if (lease != null && lease.IsValid(hardwareId))
                    {
                        _logger.LogInformation("[SUB_AUDIT] Using valid offline license lease. Plan: {PlanName}, Lifetime: {IsLifetime}", lease.PlanName, lease.IsLifetime);
                        return new LicenseCheckResult
                        {
                            IsValid = true,
                            Status = lease.IsLifetime ? "active" : "offline_valid",
                            IsLifetime = lease.IsLifetime,
                            PlanName = lease.PlanName ?? "Offline Lease",
                            PlanRank = lease.PlanRank,
                            DaysRemaining = lease.DaysRemaining
                        };
                    }
                }
                catch (Exception leaseEx)
                {
                    _logger.LogError(leaseEx, "[SUB_AUDIT] Error checking offline lease.");
                }
            }

            return LicenseCheckResult.Failure("Subscription check failed and no valid offline license lease exists.");
        }

        public async Task<LicenseCheckResult> RedeemVoucherAsync(Guid accountId, string voucherCode)
        {
            if (string.IsNullOrWhiteSpace(voucherCode))
                return LicenseCheckResult.Failure("Voucher code cannot be empty.");

            string cleanCode = voucherCode.Trim().ToUpper();
            try
            {
                _logger.LogInformation("[VOUCHER_AUDIT] Redeeming voucher {Code} for Account: {AccountId}", cleanCode, accountId);
                var parameters = new Dictionary<string, object>
                {
                    { "p_account_id", accountId },
                    { "p_voucher_code", cleanCode }
                };

                var response = await _supabase.Rpc("redeem_license_voucher", parameters);
                if (response == null || string.IsNullOrWhiteSpace(response.Content) || response.Content == "null")
                {
                    return LicenseCheckResult.Failure("No response received from voucher redemption service.");
                }

                using var doc = JsonDocument.Parse(response.Content);
                var root = doc.RootElement;

                bool success = root.TryGetProperty("success", out var scProp) && scProp.GetBoolean();
                if (success)
                {
                    string planName = root.TryGetProperty("plan_name", out var pnProp) ? (pnProp.GetString() ?? "Lifetime") : "Lifetime";
                    int tierRank = root.TryGetProperty("tier_rank", out var trProp) && trProp.TryGetInt32(out var tr) ? tr : 1;
                    if (_configService != null)
                    {
                        try
                        {
                            var existing = await _configService.LoadConfigAsync<Management.Domain.Models.LicenseLease>("license.lease");
                            var hw = existing?.HardwareId ?? Environment.MachineName;
                            var lease = new Management.Domain.Models.LicenseLease
                            {
                                HardwareId = hw,
                                AccountId = accountId,
                                BranchId = existing?.BranchId,
                                IsLifetime = true,
                                PlanRank = tierRank,
                                PlanName = planName,
                                DaysRemaining = 0,
                                ExpiryDate = DateTime.UtcNow.AddYears(100),
                                Signature = "SIGNED-" + hw
                            };
                            await _configService.SaveConfigAsync(lease, "license.lease");
                            _logger.LogInformation("[VOUCHER_AUDIT] Offline license lease upgraded to lifetime.");
                        }
                        catch (Exception leaseEx)
                        {
                            _logger.LogWarning(leaseEx, "[VOUCHER_AUDIT] Failed to save upgraded license lease.");
                        }
                    }

                    return new LicenseCheckResult
                    {
                        IsValid = true,
                        Status = "active",
                        IsLifetime = true,
                        PlanName = planName,
                        PlanRank = tierRank,
                        AccountId = accountId
                    };
                }
                else
                {
                    string error = root.TryGetProperty("error", out var errProp) ? (errProp.GetString() ?? "Redemption failed") : "Redemption failed";
                    string friendlyError = error switch
                    {
                        "INVALID_VOUCHER_CODE" => "The voucher code entered is invalid. Please check and try again.",
                        "VOUCHER_ALREADY_USED" => "This voucher code has already been redeemed.",
                        "ACCOUNT_NOT_FOUND" => "The specified account could not be found.",
                        _ => $"Voucher error: {error}"
                    };
                    _logger.LogWarning("[VOUCHER_AUDIT] ✗ Voucher redemption failed: {Error}", error);
                    return LicenseCheckResult.Failure(friendlyError);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[VOUCHER_AUDIT] Exception during voucher redemption for code: {Code}", cleanCode);
                return LicenseCheckResult.Failure($"Redemption error: {ex.Message}");
            }
        }

        public async Task<LicenseCheckResult> RegisterDeviceAsync(Guid accountId, Guid branchId, string hardwareId, string label)
        {
            if (string.IsNullOrWhiteSpace(hardwareId))
                return LicenseCheckResult.Failure("Hardware ID is required.");

            try
            {
                _logger.LogInformation("[DEVICE_REG] Registering device: {Label} (HW: {HardwareId}) for Branch {BranchId}", label, hardwareId, branchId);
                var parameters = new Dictionary<string, object>
                {
                    { "p_account_id", accountId },
                    { "p_branch_id", branchId },
                    { "p_hardware_id", hardwareId },
                    { "p_label", string.IsNullOrWhiteSpace(label) ? Environment.MachineName : label }
                };

                var response = await _supabase.Rpc("register_device", parameters);
                if (response == null || string.IsNullOrWhiteSpace(response.Content) || response.Content == "null")
                {
                    return LicenseCheckResult.Failure("No response from server during device registration.");
                }

                using var doc = JsonDocument.Parse(response.Content);
                var root = doc.RootElement;

                bool success = root.TryGetProperty("success", out var scProp) && scProp.GetBoolean();
                if (success)
                {
                    Guid? devId = root.TryGetProperty("device_id", out var devProp) && Guid.TryParse(devProp.GetString(), out var dId) ? dId : null;
                    return new LicenseCheckResult
                    {
                        IsValid = true,
                        Status = "registered",
                        DeviceId = devId,
                        AccountId = accountId,
                        BranchId = branchId,
                        FacilityName = label
                    };
                }
                else
                {
                    string err = root.TryGetProperty("error", out var errProp) ? (errProp.GetString() ?? "Registration failed") : "Registration failed";
                    return LicenseCheckResult.Failure($"Device registration rejected: {err}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[DEVICE_REG] Error registering device: {HardwareId}", hardwareId);
                return LicenseCheckResult.Failure(ex.Message);
            }
        }

        public async Task<bool> RevokeDeviceAsync(Guid deviceId, Guid accountId)
        {
            try
            {
                _logger.LogInformation("[DEVICE_REVOKE] Revoking device {DeviceId} for Account {AccountId}", deviceId, accountId);
                var parameters = new Dictionary<string, object>
                {
                    { "p_device_id", deviceId },
                    { "p_account_id", accountId }
                };

                var response = await _supabase.Rpc("revoke_device", parameters);
                if (response != null && !string.IsNullOrWhiteSpace(response.Content) && response.Content != "null")
                {
                    using var doc = JsonDocument.Parse(response.Content);
                    return doc.RootElement.TryGetProperty("success", out var scProp) && scProp.GetBoolean();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[DEVICE_REVOKE] Error revoking device {DeviceId}", deviceId);
            }
            return false;
        }

    }
}
