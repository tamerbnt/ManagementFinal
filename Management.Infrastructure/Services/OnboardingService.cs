using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Management.Domain.Primitives;
using Management.Domain.Services;
using Supabase;
using Supabase.Postgrest;
using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;
using Management.Infrastructure.Integrations.Supabase.Models;
using Serilog;
using Management.Application.Interfaces;
using Management.Application.Services;
using Management.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Management.Application.DTOs;

namespace Management.Infrastructure.Services
{
    public class LicenseValidationResult
    {
        public bool IsAssigned { get; set; }
        public Guid? TenantId { get; set; }
        public Guid? LicenseId { get; set; }
        public bool HasIndustry { get; set; }
        public bool HasOwner { get; set; }
        public bool IsLicensed { get; set; }
        public List<LicensedFacilityDto> Facilities { get; set; } = new();
    }

    public interface IOnboardingService
    {
        Task<Result<LicenseValidationResult>> ValidateLicenseAsync(string licenseKey);
        Task<Result<List<LicensedFacilityDto>>> GetLicensedFacilitiesAsync(string? licenseKey = null);
        Task<Result<Guid>> SignUpOnlyAsync(string email, string password);
        Task<Result<Guid>> RegisterBusinessAsync(Guid ownerId, string ownerName, string email, string licenseKey, string tenantName, string tenantSlug, int facilityType = 0);
        Task<Result<bool>> CheckVerificationStatusAsync(string email);
        Task<Result> ResendConfirmationEmailAsync(string email);
        Task<Result<Guid>> CompleteOnboardingAsync(Management.Application.DTOs.OnboardingState state);
        Task<Result> RegisterCurrentDeviceAsync(Guid tenantId, string label, string licenseKey);
        Task<Result<Guid?>> VerifyCurrentDeviceAsync();
        Task<Result> RevokeDeviceAsync(Guid deviceId);
        Task<Result<List<SupabaseDevice>>> GetDevicesAsync(Guid tenantId);
        Task<Result> UpdateTenantIndustryAsync(Guid tenantId, string industry);
        Task<int> GetDeviceCountAsync(Guid tenantId);
        Task<Result<Guid>> ProvisionFacilityAsync(Guid tenantId, Guid ownerId, string ownerEmail, string ownerName, int facilityType, string facilityName);
    }

    public class OnboardingService : IOnboardingService
    {
        public static string CategoryToSlug(string? category)
        {
            if (string.IsNullOrWhiteSpace(category)) return "pos_inventory";
            var clean = category.Trim().ToLowerInvariant().Replace(" ", "_").Replace("-", "_");
            return clean switch
            {
                "posinventory" or "pos_inventory" or "pos" or "order_inventory" => "pos_inventory",
                "appointmentservice" or "appointment_service" or "service" or "salon" => "appointment_service",
                "membershipsession" or "membership_session" or "gym" or "membership" => "membership_session",
                "projectmilestone" or "project_milestone" or "project" or "milestone" => "project_milestone",
                "rentalbooking" or "rental_booking" or "rental" or "booking" => "rental_booking",
                "educationcohort" or "education_cohort" or "education" or "school" => "education_cohort",
                _ => clean
            };
        }

        public static string CategoryToSlug(BusinessCategory category) => category switch
        {
            BusinessCategory.PosInventory => "pos_inventory",
            BusinessCategory.AppointmentService => "appointment_service",
            BusinessCategory.MembershipSession => "membership_session",
            BusinessCategory.ProjectMilestone => "project_milestone",
            BusinessCategory.RentalBooking => "rental_booking",
            BusinessCategory.EducationCohort => "education_cohort",
            _ => "pos_inventory"
        };

        private readonly Supabase.Client _supabase;
        private readonly ITenantService _tenantService;
        private readonly IConfigurationService _configService;
        private readonly Microsoft.Extensions.DependencyInjection.IServiceScopeFactory _scopeFactory;

        public OnboardingService(Supabase.Client supabase, ITenantService tenantService, IConfigurationService configService, Microsoft.Extensions.DependencyInjection.IServiceScopeFactory scopeFactory)
        {
            _supabase = supabase;
            _tenantService = tenantService;
            _configService = configService;
            _scopeFactory = scopeFactory;
        }

        private const int NetworkTimeoutSeconds = 45;

        public async Task<Result<List<LicensedFacilityDto>>> GetLicensedFacilitiesAsync(string? licenseKey = null)
        {
            try
            {
                var hardwareId = _tenantService.GetHardwareId();
                var keyToUse = licenseKey?.Trim();
                
                Serilog.Log.Information($"[OnboardingService] Discovering facilities for HardwareId: {hardwareId}");

                var parameters = new Dictionary<string, object>
                {
                    { "p_lookup_key", keyToUse ?? string.Empty },
                    { "p_hardware_id", hardwareId },
                    { "p_label", Environment.MachineName }
                };

                var response = await _supabase.Rpc("verify_license_key", parameters);

                if (response == null || string.IsNullOrEmpty(response.Content))
                {
                    return Result.Failure<List<LicensedFacilityDto>>(new Error("Onboarding.RpcError", "Server returned no response."));
                }

                var json = Newtonsoft.Json.Linq.JObject.Parse(response.Content);
                var isValid = json.Value<bool>("valid");
                
                if (!isValid)
                {
                    return Result.Failure<List<LicensedFacilityDto>>(new Error("Onboarding.LicenseInvalid", json.Value<string>("message") ?? "License invalid."));
                }

                var tenantIdStr = json.Value<string>("tenant_id");
                if (string.IsNullOrEmpty(tenantIdStr) || !Guid.TryParse(tenantIdStr, out var tenantId))
                {
                    return Result.Success(new List<LicensedFacilityDto>()); // No tenant yet
                }

                // FIX: Use SECURITY DEFINER RPC instead of direct table query (which is RLS-blocked)
                // Direct query returns 0 rows before JWT has tenant_id, causing fallback to static defaults
                var rpcParams = new Dictionary<string, object> { { "p_tenant_id", tenantId } };
                var facilitiesRpc = await _supabase.Rpc("get_tenant_facilities", rpcParams);

                if (facilitiesRpc == null || string.IsNullOrEmpty(facilitiesRpc.Content) || facilitiesRpc.Content == "null")
                {
                    return Result.Success(new List<LicensedFacilityDto>());
                }

                var snakeCaseSettings = new Newtonsoft.Json.JsonSerializerSettings
                {
                    ContractResolver = new Newtonsoft.Json.Serialization.DefaultContractResolver
                    {
                        NamingStrategy = new Newtonsoft.Json.Serialization.SnakeCaseNamingStrategy()
                    }
                };

                var facilityList = Newtonsoft.Json.JsonConvert.DeserializeObject<List<LicensedFacilityDto>>(facilitiesRpc.Content, snakeCaseSettings);

                return Result.Success(facilityList ?? new List<LicensedFacilityDto>());
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, "[OnboardingService] GetLicensedFacilitiesAsync failed.");
                return Result.Failure<List<LicensedFacilityDto>>(new Error("Onboarding.DiscoveryError", ex.Message));
            }
        }

        public async Task<Result<LicenseValidationResult>> ValidateLicenseAsync(string licenseKey)
        {
            try
            {
                // Step 1: Normalize key
                var normalizedKey = licenseKey?.Trim() ?? string.Empty;
                var hardwareId = _tenantService.GetHardwareId();
                
                Serilog.Log.Information($"[OnboardingService] Validating license via RPC: {normalizedKey}");

                var parameters = new Dictionary<string, object>
                {
                    { "p_lookup_key", normalizedKey },
                    { "p_hardware_id", hardwareId },
                    { "p_label", Environment.MachineName } 
                };

                var response = await _supabase.Rpc("verify_license_key", parameters);

                if (response == null || string.IsNullOrEmpty(response.Content))
                {
                    // FALLBACK: Try offline lease
                    var lease = await LoadValidLeaseAsync(hardwareId);
                    if (lease != null)
                    {
                        Serilog.Log.Information("[OnboardingService] Using valid offline license lease.");
                        return Result.Success(new LicenseValidationResult { IsLicensed = true });
                    }
                    return Result.Failure<LicenseValidationResult>(new Error("Onboarding.RpcError", "Server returned no response and no valid offline lease found."));
                }

                Newtonsoft.Json.Linq.JObject json;
                try 
                {
                    json = Newtonsoft.Json.Linq.JObject.Parse(response.Content);
                }
                catch (Exception parseEx)
                {
                     Serilog.Log.Error(parseEx, $"[OnboardingService] Failed to parse RPC response: {response.Content}");
                     return Result.Failure<LicenseValidationResult>(new Error("Onboarding.ParseError", "Failed to verify license response from server structure."));
                }

                var isValid = json.Value<bool>("valid");
                var message = json.Value<string>("message");
                
                if (!isValid)
                {
                    Serilog.Log.Warning($"[OnboardingService] License RPC Invalid: {message}");
                    return Result.Failure<LicenseValidationResult>(new Error("Onboarding.LicenseInvalid", message ?? "License invalid."));
                }

                var tenantIdStr = json.Value<string>("tenant_id");
                var licenseIdStr = json.Value<string>("license_id");
                
                Guid? tenantId = !string.IsNullOrEmpty(tenantIdStr) ? Guid.Parse(tenantIdStr) : null;
                Guid? licenseId = !string.IsNullOrEmpty(licenseIdStr) ? Guid.Parse(licenseIdStr) : null;

                // Save or refresh the local lease on successful server validation
                await SaveLicenseLeaseAsync(hardwareId);
                
                bool hasIndustry = false;
                bool hasOwner = false;
                var facilities = new List<LicensedFacilityDto>();

                if (tenantId.HasValue)
                {
                    try 
                    {
                        var tenantQ = await _supabase.From<SupabaseTenant>()
                            .Match(new Dictionary<string, string> { {"id", tenantId.Value.ToString()} })
                            .Get();
                        
                        if (tenantQ.Models.Count > 0)
                        {
                            hasIndustry = !string.IsNullOrEmpty(tenantQ.Models[0].Industry);
                        }

                        var staffQ = await _supabase.From<SupabaseStaffMember>()
                            .Filter("tenant_id", Supabase.Postgrest.Constants.Operator.Equals, tenantId.Value.ToString())
                            .Filter("role", Supabase.Postgrest.Constants.Operator.Equals, (int)StaffRole.Owner)
                            .Get();
                        
                        hasOwner = staffQ.Models.Count > 0;

                        // NEW: Fetch facilities as part of validation
                        var facilitiesQ = await _supabase.From<SupabaseFacility>()
                            .Filter("tenant_id", Supabase.Postgrest.Constants.Operator.Equals, tenantId.Value.ToString())
                            .Get();
                        
                        facilities = facilitiesQ.Models.Select(f => new LicensedFacilityDto
                        {
                            Id = f.Id,
                            Name = f.Name,
                            Type = f.Type
                        }).ToList();
                    }
                    catch (Exception dbEx)
                    {
                        Serilog.Log.Warning(dbEx, $"[OnboardingService] Optional identity checks failed (likely RLS): {dbEx.Message}");
                    }
                }

                var result = new LicenseValidationResult
                {
                    IsAssigned = tenantId.HasValue,
                    TenantId = tenantId,
                    LicenseId = licenseId,
                    HasIndustry = hasIndustry,
                    HasOwner = hasOwner,
                    IsLicensed = true,
                    Facilities = facilities
                };

                return Result.Success(result);
            }
            catch (TimeoutException)
            {
                return Result.Failure<LicenseValidationResult>(new Error("Onboarding.NetworkTimeout", "Connection timed out. Please check your internet connection."));
            }
            catch (Exception ex)
            {
                return Result.Failure<LicenseValidationResult>(new Error("Onboarding.Error", $"License check failed: {ex.Message}"));
            }
        }

        public async Task<int> GetDeviceCountAsync(Guid tenantId)
        {
            try 
            {
                var response = await _supabase.From<SupabaseDevice>()
                    .Filter("tenant_id", Supabase.Postgrest.Constants.Operator.Equals, tenantId.ToString())
                    .Get();
                return response.Models.Count;
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, $"Failed to get device count for tenant {tenantId}");
                return 0;
            }
        }

        public async Task<Result<Guid>> CompleteOnboardingAsync(Management.Application.DTOs.OnboardingState state)
        {
            var signUpResult = await SignUpOnlyAsync(state.AdminEmail, state.AdminPassword);
            if (signUpResult.IsFailure)
            {
                return Result.Failure<Guid>(signUpResult.Error);
            }

            var ownerId = signUpResult.Value;
            var hardwareId = _tenantService.GetHardwareId();
            var rawCategory = !string.IsNullOrWhiteSpace(state.Category) ? state.Category : state.FacilityType;
            var categorySlug = CategoryToSlug(rawCategory);
            state.Category = categorySlug;
            var branchName = !string.IsNullOrWhiteSpace(state.BranchName) ? state.BranchName.Trim() : "Main Branch";
            var voucher = !string.IsNullOrWhiteSpace(state.VoucherCode) ? state.VoucherCode.Trim().ToUpper() : (!string.IsNullOrWhiteSpace(state.LicenseKey) ? state.LicenseKey.Trim().ToUpper() : null);
            // Safety-net: "TRIAL" is a local sentinel, never a real voucher code.
            // Discard it so the RPC takes the free-trial path (no p_voucher_code sent).
            if (voucher == "TRIAL") voucher = null;

            Serilog.Log.Information($"[OnboardingService] Phase 2 Genesis: Calling onboard_owner_account for Owner {ownerId}, Category: {categorySlug}, Voucher: {voucher ?? "<NONE>"}");

            try
            {
                var parameters = new Dictionary<string, object>
                {
                    { "p_owner_id", ownerId },
                    { "p_full_name", state.AdminFullName ?? "Owner" },
                    { "p_email", state.AdminEmail.Trim().ToLowerInvariant() },
                    { "p_business_name", state.BusinessName ?? "My Business" },
                    { "p_branch_name", branchName },
                    { "p_category", categorySlug },
                    { "p_hardware_id", hardwareId },
                    { "p_device_label", Environment.MachineName }
                };

                if (!string.IsNullOrWhiteSpace(state.Address))
                {
                    parameters["p_address"] = state.Address.Trim();
                }

                if (!string.IsNullOrWhiteSpace(state.Phone))
                {
                    parameters["p_phone"] = state.Phone.Trim();
                }

                if (!string.IsNullOrWhiteSpace(voucher))
                {
                    parameters["p_voucher_code"] = voucher;
                }

                var rpcTask = _supabase.Rpc("onboard_owner_account", parameters);
                var rpcResponse = await rpcTask.WaitAsync(TimeSpan.FromSeconds(NetworkTimeoutSeconds));

                if (rpcResponse == null || string.IsNullOrWhiteSpace(rpcResponse.Content) || rpcResponse.Content == "null")
                {
                    return Result.Failure<Guid>(new Error("Onboarding.RpcFailed", "No response from account onboarding service."));
                }

                using var doc = System.Text.Json.JsonDocument.Parse(rpcResponse.Content);
                var root = doc.RootElement;

                bool success = root.TryGetProperty("success", out var sc) && sc.GetBoolean();
                if (!success)
                {
                    string err = root.TryGetProperty("error", out var ep) ? (ep.GetString() ?? "Onboarding failed") : "Onboarding failed";
                    string friendly = err switch
                    {
                        "ACCOUNT_ALREADY_EXISTS" => "An account with this owner ID already exists.",
                        "INVALID_VOUCHER_CODE" => "The provided voucher code is invalid.",
                        "VOUCHER_ALREADY_USED" => "The provided voucher code has already been redeemed.",
                        _ => $"Onboarding rejected: {err}"
                    };
                    return Result.Failure<Guid>(new Error("Onboarding.Rejected", friendly));
                }

                Guid? branchId = root.TryGetProperty("branch_id", out var bp) && Guid.TryParse(bp.GetString(), out var bId) ? bId : null;
                bool isLifetime = root.TryGetProperty("is_lifetime", out var lp) && lp.GetBoolean();
                string planStatus = root.TryGetProperty("plan_status", out var pp) ? (pp.GetString() ?? "trialing") : "trialing";

                _tenantService.SetAccountId(ownerId);

                try
                {
                    var lease = new Management.Domain.Models.LicenseLease
                    {
                        HardwareId = hardwareId,
                        AccountId = ownerId,
                        BranchId = branchId,
                        IsLifetime = isLifetime,
                        PlanName = isLifetime ? "Lifetime" : "Evaluation Trial",
                        ExpiryDate = isLifetime ? DateTime.UtcNow.AddYears(100) : DateTime.UtcNow.AddDays(14),
                        Signature = "SIGNED-" + hardwareId
                    };
                    await _configService.SaveConfigAsync(lease, "license.lease");
                    Serilog.Log.Information("[OnboardingService] Phase 2 license lease saved locally.");
                }
                catch (Exception leaseEx)
                {
                    Serilog.Log.Warning(leaseEx, "[OnboardingService] Failed to save license lease.");
                }

                try
                {
                    await Task.Delay(1000);
                    await _supabase.Auth.RefreshSession();
                }
                catch { }

                return Result.Success(ownerId);
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, "[OnboardingService] CompleteOnboardingAsync exception.");
                return Result.Failure<Guid>(new Error("Onboarding.Exception", ex.Message));
            }
        }

        public async Task<Result<Guid>> SignUpOnlyAsync(string email, string password)
        {
            email = email?.Trim().ToLowerInvariant() ?? string.Empty;
            password = password?.Trim() ?? string.Empty;
            try
            {
                Serilog.Log.Information($"[OnboardingService] SIGN-UP ONLY Phase started for {email}");

                Guid? ownerId = null;
                try
                {
                    // 1. Sign Up User (Triggers handle_new_user_setup)
                    var signUpTask = _supabase.Auth.SignUp(email, password);
                    var session = await signUpTask.WaitAsync(TimeSpan.FromSeconds(NetworkTimeoutSeconds));
                    
                    if (session?.User != null)
                    {
                        ownerId = Guid.Parse(session.User.Id);
                    }
                }
                catch (Exception ex) when (ex.Message.Contains("already registered") || ex.Message.Contains("422") || ex.Message.Contains("already exists"))
                {
                    Serilog.Log.Information("[OnboardingService] User already exists in Auth. Attempting to recover session...");
                    try 
                    {
                        var signInSession = await _supabase.Auth.SignIn(email, password);
                        if (signInSession?.User != null)
                        {
                            ownerId = Guid.Parse(signInSession.User.Id!);
                            Serilog.Log.Information($"[OnboardingService] Recovered OwnerId: {ownerId}");
                        }
                    }
                    catch (Exception signInEx)
                    {
                        Serilog.Log.Error(signInEx, "[OnboardingService] Failed to recover existing user session.");
                        return Result.Failure<Guid>(new Error("Onboarding.UserExists", "An account with this email already exists and could not be recovered. Please use a different email or contact support."));
                    }
                }

                if (!ownerId.HasValue)
                {
                    return Result.Failure<Guid>(new Error("Onboarding.SignUpFailed", "Could not create user account."));
                }

                Serilog.Log.Information($"[OnboardingService] Auth Sign-Up successful. OwnerId: {ownerId}");

                // 2. Automated Identity Sync
                // We wait for the trigger to finish so the UI has immediate data
                Serilog.Log.Information("[OnboardingService] Waiting for Automated Identity Sync...");
                bool isVisible = false;
                for (int v = 0; v < 10; v++)
                {
                    try
                    {
                        var profileCheck = await _supabase.From<SupabaseProfile>()
                            .Filter("id", Supabase.Postgrest.Constants.Operator.Equals, ownerId.ToString())
                            .Get();
                        
                        if (profileCheck.Models.Count > 0)
                        {
                            isVisible = true;
                            Serilog.Log.Information($"[OnboardingService] Identity Automation Verified (Attempt {v+1}).");
                            break;
                        }
                    }
                    catch (Exception) { /* Silent retry */ }
                    
                    await Task.Delay(1000); 
                }

                if (!isVisible)
                {
                    Serilog.Log.Warning("[OnboardingService] Identity sync is slow. Returning success for optimistic UI...");
                }

                return Result.Success(ownerId.Value);
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, $"[OnboardingService] SignUpOnlyAsync Failure: {ex.Message}");
                return Result.Failure<Guid>(new Error("Onboarding.SignUpError", ex.Message));
            }
        }

        public async Task<Result<Guid>> RegisterBusinessAsync(Guid ownerId, string ownerName, string email, string licenseKey, string tenantName, string tenantSlug, int facilityType = 0)
        {
            var state = new OnboardingState
            {
                AdminFullName = ownerName,
                AdminEmail = email,
                BusinessName = tenantName,
                LicenseKey = licenseKey,
                VoucherCode = licenseKey
            };
            return await CompleteOnboardingAsync(state);
        }

        public async Task<Result> UpdateTenantIndustryAsync(Guid tenantId, string industry)
        {
            try
            {
                Serilog.Log.Information($"[OnboardingService] Updating Industry to '{industry}' for Tenant: {tenantId}");
                
                var model = new SupabaseTenant 
                { 
                    Id = tenantId, 
                    Industry = industry 
                };

                await _supabase.From<SupabaseTenant>().Update(model);

                return Result.Success();
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, $"[OnboardingService] Failed to update industry for tenant {tenantId}");
                return Result.Failure(new Error("Onboarding.UpdateError", $"Failed to update business type: {ex.Message}"));
            }
        }

        public async Task<Result> RegisterCurrentDeviceAsync(Guid tenantId, string label, string licenseKey)
        {
            // NOTE (Phase 2): The old verify_license_key RPC no longer exists.
            // For first-time onboarding the device is already registered atomically inside
            // onboard_owner_account. This method now verifies the device is active via
            // check_device_registration and saves the local lease — no re-registration needed.
            try
            {
                var hardwareId = _tenantService.GetHardwareId();
                Serilog.Log.Information($"[OnboardingService] Verifying device registration: {label} (HW: {hardwareId}) for Tenant: {tenantId}");

                var parameters = new Dictionary<string, object>
                {
                    { "p_hardware_id", hardwareId }
                };

                var response = await _supabase.Rpc("check_device_registration", parameters);

                if (response == null || string.IsNullOrEmpty(response.Content) || response.Content == "null")
                {
                    // Device may not be committed yet — save lease optimistically and succeed.
                    Serilog.Log.Warning("[OnboardingService] check_device_registration returned no content. Saving lease optimistically.");
                    await SaveLicenseLeaseAsync(hardwareId);
                    return Result.Success();
                }

                using var doc = System.Text.Json.JsonDocument.Parse(response.Content);
                var root = doc.RootElement;
                bool isRegistered = root.TryGetProperty("is_registered", out var rp) && rp.GetBoolean();
                bool isActive     = !root.TryGetProperty("is_active", out var ap) || ap.GetBoolean();

                if (isRegistered && !isActive)
                {
                    Serilog.Log.Error("[OnboardingService] Device is registered but marked inactive.");
                    return Result.Failure(new Error("Onboarding.DeviceInactive", "This device has been deactivated. Contact your administrator."));
                }

                // Registered & active (or brand-new — onboard_owner_account already inserted it)
                Serilog.Log.Information("[OnboardingService] Device verified successfully.");
                await SaveLicenseLeaseAsync(hardwareId);
                return Result.Success();
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, $"[OnboardingService] Device verification exception for tenant {tenantId}");
                // Non-fatal for onboarding: device was already registered by onboard_owner_account.
                // Save the lease and succeed so the user is not blocked.
                try { await SaveLicenseLeaseAsync(_tenantService.GetHardwareId()); } catch { }
                return Result.Success();
            }
        }

        public async Task<Result<bool>> CheckVerificationStatusAsync(string email)
        {
            email = email?.Trim().ToLowerInvariant() ?? string.Empty;
            try
            {
                // We can check if the profile exists in the public schema. 
                // Our handle_new_user trigger creates the profile on auth.user creation.
                // However, we want to know if they 'ACTUALLY' confirmed.
                // Supabase doesn't expose 'is_confirmed' easily via public API for other users.
                // But we can try to SignIn with a dummy password or just check the profile's 'is_verified' flag if we add one.
                
                // For now, let's assume if they show up in a 'verified_owners' view or similar.
                // Or better, we just Query the profiles table.
                var response = await _supabase.From<SupabaseProfile>()
                    .Filter("email", Supabase.Postgrest.Constants.Operator.Equals, email)
                    .Get();

                if (response.Models.Count > 0)
                {
                    // In a production system, the trigger should set a 'is_confirmed' flag 
                    // or we check the auth.users table via an RPC.
                    return Result.Success(true); 
                }
                
                return Result.Success(false);
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, $"[OnboardingService] Failed to check verification status for {email}");
                return Result.Failure<bool>(new Error("Onboarding.CheckFailed", "Could not verify account status."));
            }
        }

        public async Task<Result> ResendConfirmationEmailAsync(string email)
        {
            try
            {
                Serilog.Log.Information($"[OnboardingService] Resending verification email to {email}");
                
                // Older Supabase clients don't have a dedicated Resend method.
                // The documented workaround is to call SignUp again with the same email.
                // Supabase will detect the existing account and resend the confirmation email.
                // This will throw an exception about "User already registered", which we catch and treat as success.
                try
                {
                    await _supabase.Auth.SignUp(email, Guid.NewGuid().ToString()); // Dummy password
                }
                catch (Exception ex) when (ex.Message.Contains("already registered") || ex.Message.Contains("User already registered"))
                {
                    // This is expected - Supabase has resent the email
                    Serilog.Log.Information($"[OnboardingService] Verification email resent successfully to {email}");
                }
                
                return Result.Success();
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, $"[OnboardingService] Failed to resend verification email to {email}");
                return Result.Failure(new Error("Onboarding.ResendFailed", $"Failed to resend email: {ex.Message}"));
            }
        }

        public async Task<Result<Guid?>> VerifyCurrentDeviceAsync()
        {
            var hardwareId = _tenantService.GetHardwareId();

            try
            {
                var parameters = new Dictionary<string, object>
                {
                    { "p_hardware_id", hardwareId }
                };

                var response = await _supabase.Rpc("check_device_registration", parameters);
                
                if (response != null && !string.IsNullOrEmpty(response.Content) && response.Content != "null")
                {
                    using var doc = System.Text.Json.JsonDocument.Parse(response.Content);
                    var root = doc.RootElement;
                    bool isRegistered = root.TryGetProperty("is_registered", out var rp) && rp.GetBoolean();
                    
                    if (isRegistered)
                    {
                        bool isActive = !root.TryGetProperty("is_active", out var ap) || ap.GetBoolean();
                        bool accountIsActive = !root.TryGetProperty("account_is_active", out var aap) || aap.GetBoolean();
                        
                        if (isActive && accountIsActive && root.TryGetProperty("account_id", out var accProp) && Guid.TryParse(accProp.GetString(), out var accountId))
                        {
                            await SaveLicenseLeaseAsync(hardwareId);
                            return Result.Success<Guid?>(accountId);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Serilog.Log.Warning($"[OnboardingService] Server activation check failed (RPC): {ex.Message}. Falling back to offline lease.");
            }

            var lease = await LoadValidLeaseAsync(hardwareId);
            if (lease != null)
            {
                return Result.Success<Guid?>(lease.AccountId); 
            }

            return Result.Success<Guid?>(null);
        }

        public async Task<Result> RevokeDeviceAsync(Guid deviceId)
        {
            try
            {
                Serilog.Log.Information($"[OnboardingService] Revoking device: {deviceId}");
                
                var parameters = new Dictionary<string, object>
                {
                    { "p_device_id", deviceId }
                };

                await _supabase.Rpc("revoke_device", parameters);
                return Result.Success();
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, $"[OnboardingService] Failed to revoke device {deviceId}");
                return Result.Failure(new Error("Onboarding.RevokeError", $"Failed to revoke device: {ex.Message}"));
            }
        }

        public async Task<Result<List<SupabaseDevice>>> GetDevicesAsync(Guid tenantId)
        {
            try
            {
                var response = await _supabase.From<SupabaseDevice>()
                    .Where(x => x.TenantId == tenantId)
                    .Get();

                return Result.Success(response.Models);
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, $"[OnboardingService] Failed to fetch devices for tenant {tenantId}");
                return Result.Failure<List<SupabaseDevice>>(new Error("Onboarding.FetchError", $"Failed to fetch devices: {ex.Message}"));
            }
        }

        private Dictionary<string, bool> GetOwnerPermissions()
        {
            return new Dictionary<string, bool>
            {
                { "can_manage_staff", true },
                { "can_manage_finance", true },
                { "can_manage_settings", true },
                { "can_delete_data", true },
                { "can_access_admin_panel", true }
            };
        }

        private async Task SaveLicenseLeaseAsync(string hardwareId)
        {
            try
            {
                var existing = await _configService.LoadConfigAsync<Management.Domain.Models.LicenseLease>("license.lease");
                var lease = new Management.Domain.Models.LicenseLease
                {
                    HardwareId = hardwareId,
                    AccountId = existing?.AccountId,
                    BranchId = existing?.BranchId,
                    PlanRank = existing?.PlanRank ?? 0,
                    PlanName = existing?.PlanName ?? "Evaluation Trial",
                    IsLifetime = existing?.IsLifetime ?? false,
                    ExpiryDate = existing?.ExpiryDate ?? DateTime.UtcNow.AddDays(14),
                    Signature = "SIGNED-" + hardwareId
                };

                await _configService.SaveConfigAsync(lease, "license.lease");
                Serilog.Log.Information("[OnboardingService] License lease saved locally (30d validity).");
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, "[OnboardingService] Failed to save license lease");
            }
        }

        private async Task<Management.Domain.Models.LicenseLease?> LoadValidLeaseAsync(string hardwareId)
        {
            try
            {
                var lease = await _configService.LoadConfigAsync<Management.Domain.Models.LicenseLease>("license.lease");
                if (lease != null && lease.IsValid(hardwareId))
                {
                    return lease;
                }
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, "[OnboardingService] Failed to load license lease");
            }
            return null;
        }

        public async Task<Result<Guid>> ProvisionFacilityAsync(Guid tenantId, Guid ownerId, string ownerEmail, string ownerName, int facilityType, string facilityName)
        {
            try
            {
                Serilog.Log.Information("[Onboarding] Provisioning facility {FacilityName} (Type: {Type}) for Tenant {TenantId}", 
                    facilityName, facilityType, tenantId);

                var parameters = new Dictionary<string, object>
                {
                    { "p_tenant_id", tenantId },
                    { "p_owner_id", ownerId },
                    { "p_owner_email", ownerEmail },
                    { "p_owner_name", ownerName }, // FIX: ADDED MISSING PARAMETER
                    { "p_facility_type", facilityType },
                    { "p_facility_name", facilityName }
                };

                var response = await _supabase.Rpc("fn_provision_facility", parameters);

                if (response == null || string.IsNullOrEmpty(response.Content))
                {
                    return Result.Failure<Guid>(new Error("Onboarding.ProvisionError", "Server returned no response from provisioning RPC."));
                }

                // Robust Parsing: Handle direct UUID string or JSON object
                string? facilityIdStr = null;
                if (response.Content.Trim().StartsWith("{"))
                {
                    var json = Newtonsoft.Json.Linq.JObject.Parse(response.Content);
                    if (json.Value<bool>("success"))
                    {
                        facilityIdStr = json.Value<string>("facility_id");
                    }
                    else 
                    {
                        var msg = json.Value<string>("message") ?? "Provisioning failed.";
                        return Result.Failure<Guid>(new Error("Onboarding.ProvisionError", msg));
                    }
                }
                else 
                {
                    // Direct UUID string return
                    facilityIdStr = response.Content.Trim('"');
                }

                if (Guid.TryParse(facilityIdStr, out var facilityId))
                {
                    Serilog.Log.Information("[Onboarding] Facility provisioned successfully: {FacilityId}", facilityId);

                    // STEP 2: Write to local SQLite immediately
                    try 
                    {
                        using var scope = _scopeFactory.CreateScope();
                        var context = scope.ServiceProvider.GetRequiredService<Management.Infrastructure.Data.AppDbContext>();
                        
                        var localFacility = new Management.Domain.Models.Facility
                        {
                            Id = facilityId,
                            TenantId = tenantId,
                            Type = (Management.Domain.Enums.FacilityType)facilityType,
                            Name = facilityName,
                            IsActive = true
                        };
                        
                        var existing = await context.Facilities.IgnoreQueryFilters().FirstOrDefaultAsync(f => f.Id == facilityId);
                        if (existing == null)
                        {
                            context.Facilities.Add(localFacility);
                            await context.SaveChangesAsync();
                            Serilog.Log.Information("[Setup] Facility written to local SQLite immediately: {Type} Id={Id}", facilityType, localFacility.Id);
                        }
                    }
                    catch (Exception sqlEx)
                    {
                        // Note: There is NO 'Tenants' table locally, so FK violations on TenantId are impossible. 
                        Serilog.Log.Error(sqlEx, "[Setup] Immediate local SQLite facility write failed (Sync will catch up later).");
                    }

                    return Result.Success(facilityId);
                }

                return Result.Failure<Guid>(new Error("Onboarding.ProvisionError", "Invalid Facility ID returned from server."));
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, "[Onboarding] Exception during facility provisioning");
                return Result.Failure<Guid>(new Error("Onboarding.ProvisionException", ex.Message));
            }
        }

        private StaffDto MapToDto(SupabaseStaffMember model)
        {
            return new StaffDto
            {
                Id = model.Id,
                TenantId = model.TenantId,
                FullName = model.FullName,
                Email = model.Email,
                Role = Enum.IsDefined(typeof(StaffRole), model.Role) ? (StaffRole)model.Role : StaffRole.Staff,
                Status = model.IsActive ? StaffStatus.Active : StaffStatus.Inactive,
                Permissions = GeneratePermissionsForRole(Enum.IsDefined(typeof(StaffRole), model.Role) ? (StaffRole)model.Role : StaffRole.Staff)
            };
        }

        private List<PermissionDto> GeneratePermissionsForRole(StaffRole role)
        {
            var perms = new List<PermissionDto>();
            perms.Add(new PermissionDto("View Dashboard", true));

            // All staff can view members and check-in
            perms.Add(new PermissionDto("View Members", true));
            perms.Add(new PermissionDto("Check-In", true));

            if (role == StaffRole.Owner)
            {
                perms.Add(new PermissionDto("System Settings", true));
                perms.Add(new PermissionDto("Manage Staff", true));
                perms.Add(new PermissionDto("Manage Members", true));
                perms.Add(new PermissionDto("View Finance", true));
            }

            return perms;
        }
    }
}
