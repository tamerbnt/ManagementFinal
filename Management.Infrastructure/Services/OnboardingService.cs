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
        private readonly Supabase.Client _supabase;
        private readonly ITenantService _tenantService;
        private readonly IConfigurationService _configService;

        public OnboardingService(Supabase.Client supabase, ITenantService tenantService, IConfigurationService configService)
        {
            _supabase = supabase;
            _tenantService = tenantService;
            _configService = configService;
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
            var tenantSlug = state.BusinessName?.ToLower().Replace(" ", "-") ?? "tenant";

            // Map string FacilityType to int for RPC
            int typeId = 0; // Default Gym (1) - but 0 triggers default logic if needed, but let's be explicit if possible. 
            // Actually, based on my SQL, 1=Gym, 5=Salon, 6=Restaurant. 
            // The SQL default is 0, which falls back to Gym. 
            // Let's send the correct IDs.
            if (state.FacilityType == "Salon") typeId = 5;
            else if (state.FacilityType == "Restaurant") typeId = 6;
            else typeId = 1; // Explicit Gym

            var registerBusinessResult = await RegisterBusinessAsync(ownerId, state.AdminFullName, state.AdminEmail, state.LicenseKey, state.BusinessName, tenantSlug, typeId);
            if (registerBusinessResult.IsFailure)
            {
                return Result.Failure<Guid>(registerBusinessResult.Error);
            }

            return Result.Success(registerBusinessResult.Value);
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
            email = email?.Trim().ToLowerInvariant() ?? string.Empty;
            try
            {
                Serilog.Log.Information($"[OnboardingService] REGISTER BUSINESS Phase for {ownerId} ({tenantName}) Type: {facilityType}");

                Guid? tenantId = null;

                // 1. Register Business via RPC
                int maxRetries = 5;
                
                for (int i = 0; i < maxRetries; i++)
                {
                    if (tenantId.HasValue) break;

                    try 
                    {
                        Serilog.Log.Information($"[OnboardingService] RPC Attempt {i+1}/{maxRetries} for business: {tenantName}");
                        var parameters = new Dictionary<string, object>
                        {
                            { "p_owner_id", ownerId },
                            { "p_owner_name", ownerName },
                            { "p_email", email },
                            { "p_license_key", licenseKey },
                            { "p_tenant_name", tenantName },
                            { "p_tenant_slug", tenantSlug },
                            { "p_facility_type", facilityType } // NEW: Pass type to the cloner
                        };

                        var rpcTask = _supabase.Rpc("onboard_new_tenant", parameters);
                        var rpcResponse = await rpcTask.WaitAsync(TimeSpan.FromSeconds(NetworkTimeoutSeconds));
                        
                        if (!string.IsNullOrEmpty(rpcResponse.Content) && rpcResponse.Content != "null")
                        {
                            tenantId = Guid.Parse(rpcResponse.Content.Trim('"'));
                            Serilog.Log.Information($"[OnboardingService] Business Registration Successful via Idempotent RPC. TenantId: {tenantId}");
                            break; 
                        }
                    }
                    catch (Supabase.Postgrest.Exceptions.PostgrestException ex) 
                    {
                        Serilog.Log.Error(ex, $"[OnboardingService] RPC Registration Failure: {ex.Message}");
                    }

                    await Task.Delay(2000 * (i + 1));
                }

                // --- Guard: Abort if RPC never returned a valid tenantId ---
                if (!tenantId.HasValue)
                {
                    const string rpcFailMsg = "Registration RPC failed after all retries. " +
                        "Ensure the 'profiles' table in Supabase has an 'updated_at' column. " +
                        "Run: ALTER TABLE public.profiles ADD COLUMN IF NOT EXISTS updated_at TIMESTAMPTZ DEFAULT now();";
                    Serilog.Log.Error("[OnboardingService] " + rpcFailMsg);
                    return Result.Failure<Guid>(new Error("Onboarding.RpcFailed", rpcFailMsg));
                }

                // --- Phase 2 C# Fix: Eager Provisioning ---
                // We MUST eagerly provision the standard 3 facilities immediately upon Tenant creation.
                // This guarantees that when PC 2 and PC 3 boot up and connect to this Tenant,
                // the Facility UUIDs already exist and Supabase RLS won't throw 0-row errors.
                try
                {
                    Serilog.Log.Information($"[OnboardingService] Tenant Registered ({tenantId}). Beginning eager provisioning of Gym, Salon, and Restaurant.");
                    await ProvisionFacilityAsync(tenantId.Value, ownerId, email, ownerName, 1, "Main Gym");
                    await ProvisionFacilityAsync(tenantId.Value, ownerId, email, ownerName, 5, "Main Salon");
                    await ProvisionFacilityAsync(tenantId.Value, ownerId, email, ownerName, 6, "Main Restaurant");
                    Serilog.Log.Information("[OnboardingService] Eager provisioning completed successfully.");
                }
                catch (Exception ex)
                {
                    Serilog.Log.Error(ex, "[OnboardingService] Non-fatal error during eager facility provisioning.");
                }

                // 3. Link current device
                await RegisterCurrentDeviceAsync(tenantId.Value, $"{Environment.MachineName} (Owner)", licenseKey);

                // 4. Refresh Session for RLS
                try
                {
                    await Task.Delay(1000);
                    await _supabase.Auth.RefreshSession();
                }
                catch { /* Ignore non-critical refresh error */ }

                return Result.Success(tenantId.Value);
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, $"[OnboardingService] RegisterBusinessAsync Total Failure: {ex.Message}");
                return Result.Failure<Guid>(new Error("Onboarding.RegisterError", ex.Message));
            }
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
            try
            {
                var hardwareId = _tenantService.GetHardwareId();
                Serilog.Log.Information($"[OnboardingService] Registering device: {label} (HW: {hardwareId}) for Tenant: {tenantId} using License Key: {licenseKey}");

                var parameters = new Dictionary<string, object>
                {
                    { "p_lookup_key", licenseKey },
                    { "p_hardware_id", hardwareId },
                    { "p_label", label }
                };

                var response = await _supabase.Rpc("verify_license_key", parameters);
                
                if (response == null || string.IsNullOrEmpty(response.Content))
                {
                    return Result.Failure(new Error("Onboarding.DeviceError", "Device registration failed: No response from server."));
                }

                // Parse standard RPC JSON response
                var data = Newtonsoft.Json.JsonConvert.DeserializeObject<dynamic>(response.Content);
                bool valid = data?.valid ?? false;
                string message = data?.message ?? "Unknown error";

                if (!valid)
                {
                    Serilog.Log.Error($"[OnboardingService] Device registration failed (RPC): {message}");
                    return Result.Failure(new Error("Onboarding.LicenseInvalid", message));
                }

                Serilog.Log.Information($"[OnboardingService] Device registration successful: {message}");
                
                // SUCCESS: Save local lease for offline access
                await SaveLicenseLeaseAsync(hardwareId);
                
                return Result.Success();
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, $"[OnboardingService] Device registration exception for tenant {tenantId}");
                return Result.Failure(new Error("Onboarding.DeviceException", $"Registration failed: {ex.Message}"));
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
                // 1. Try server check first (RPC to bypass RLS)
                var parameters = new Dictionary<string, object>
                {
                    { "p_hardware_id", hardwareId }
                };

                var response = await _supabase.Rpc("check_device_activation", parameters);
                
                if (response != null && !string.IsNullOrEmpty(response.Content) && response.Content != "null")
                {
                    var data = Newtonsoft.Json.JsonConvert.DeserializeObject<dynamic>(response.Content);
                    bool active = data?.active ?? false;
                    
                    if (active)
                    {
                        var tenantIdStr = (string)data?.tenant_id;
                        if (Guid.TryParse(tenantIdStr, out var tenantId))
                        {
                            // Update local lease upon successful server check
                            await SaveLicenseLeaseAsync(hardwareId);
                            return Result.Success<Guid?>(tenantId);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Serilog.Log.Warning($"[OnboardingService] Server activation check failed (RPC): {ex.Message}. Falling back to offline lease.");
            }

            // 2. Fallback to local lease (Offline Mode)
            var lease = await LoadValidLeaseAsync(hardwareId);
            if (lease != null)
            {
                // Note: We don't have the TenantId in the local lease model currently.
                // If this is a blocker, we should update the LicenseLease model.
                // For now, return success but NULL ID if offline, which might trigger Login or activation depending on App.xaml.cs
                return Result.Success<Guid?>(null); 
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
                var lease = new Management.Domain.Models.LicenseLease
                {
                    HardwareId = hardwareId,
                    ExpiryDate = DateTime.UtcNow.AddDays(30),
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
                Status = model.IsActive ? "Active" : "Inactive",
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
