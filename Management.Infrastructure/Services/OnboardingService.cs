using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Management.Domain.Primitives;
using Management.Domain.Services;
using Supabase;
using Supabase.Postgrest;
using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace Management.Infrastructure.Services
{
    public class LicenseValidationResult
    {
        public bool IsAssigned { get; set; }
        public Guid? TenantId { get; set; }
        // We might need tenant name for the expansion flow message
    }

    public interface IOnboardingService
    {
        Task<Result<LicenseValidationResult>> ValidateLicenseAsync(string licenseKey);
        Task<Result<Guid>> CreateAccountAndOnboardAsync(string email, string password, string licenseKey, string tenantName, string tenantSlug);
        Task<Result> RegisterCurrentDeviceAsync(Guid tenantId, string label);
        Task<int> GetDeviceCountAsync(Guid tenantId);
    }

    public class OnboardingService : IOnboardingService
    {
        private readonly Supabase.Client _supabase;
        private readonly ITenantService _tenantService;

        public OnboardingService(Supabase.Client supabase, ITenantService tenantService)
        {
            _supabase = supabase;
            _tenantService = tenantService;
        }

        private const int NetworkTimeoutSeconds = 15;

        public async Task<Result<LicenseValidationResult>> ValidateLicenseAsync(string licenseKey)
        {
            try
            {
                // Step 1: Normalize key
                var normalizedKey = licenseKey?.Trim().ToUpperInvariant() ?? string.Empty;
                Serilog.Log.Information($"[OnboardingService] Validating license: {normalizedKey}");

                // Step 2: Query license and including potential tenant info (via join or separate query if needed)
                // For now, let's just get the LicenseModel which has TenantId
                var task = _supabase.From<LicenseModel>()
                    .Filter("license_key", Constants.Operator.Equals, normalizedKey)
                    .Get();

                var response = await task.WaitAsync(TimeSpan.FromSeconds(NetworkTimeoutSeconds));
                Serilog.Log.Information($"[OnboardingService] License query returned {response.Models.Count} results.");

                if (response.Models.Count == 0 || response.Models[0] == null)
                {
                    Serilog.Log.Error($"[OnboardingService] License not found: {normalizedKey}");
                    return Result.Failure<LicenseValidationResult>(new Error("Onboarding.InvalidLicense", "The license key provided could not be found. Please check your entry and try again. [Reference: CODE-P0001]"));
                }

                // Check for duplicates or partial assignments
                LicenseModel? selectedLicense = null;
                foreach (var model in response.Models)
                {
                    Serilog.Log.Information($"[OnboardingService] License Record: Key={model.LicenseKey}, Tenant={model.TenantId}");
                    if (selectedLicense == null && !model.TenantId.HasValue)
                    {
                        selectedLicense = model;
                    }
                }

                // If all are assigned, pick the first one to report assignment
                selectedLicense ??= response.Models[0];

                var result = new LicenseValidationResult
                {
                    IsAssigned = selectedLicense.TenantId.HasValue,
                    TenantId = selectedLicense.TenantId
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
                var response = await _supabase.From<TenantDeviceModel>()
                    .Filter("tenant_id", Constants.Operator.Equals, tenantId)
                    .Get();
                return response.Models.Count;
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, $"Failed to get device count for tenant {tenantId}");
                return 0;
            }
        }

        public async Task<Result<Guid>> CreateAccountAndOnboardAsync(string email, string password, string licenseKey, string tenantName, string tenantSlug)
        {
            try
            {
                // 1. Sign up the user
                Serilog.Log.Information($"[OnboardingService] Attempting Sign-Up for user: {email}");
                Guid? ownerId = null;
                try 
                {
                    var signUpTask = _supabase.Auth.SignUp(email, password);
                    var session = await signUpTask.WaitAsync(TimeSpan.FromSeconds(NetworkTimeoutSeconds));
                    if (session?.User != null)
                    {
                        ownerId = Guid.Parse(session.User.Id!);
                    }
                }
                catch (Exception ex) when (ex.Message.Contains("already registered") || ex.Message.Contains("422"))
                {
                    Serilog.Log.Warning("[OnboardingService] User already exists. Attempting to proceed with RPC for linked license check.");
                    // For security, Supabase might not give the ID. If it doesn't, we might need to tell the user to log in.
                    return Result.Failure<Guid>(new Error("Onboarding.UserExists", "An account with this email already exists. Please use the 'Login' flow or use a different email."));
                }
                
                if (!ownerId.HasValue)
                {
                    return Result.Failure<Guid>(new Error("Onboarding.SignUpFailed", "Account creation failed. Please check your credentials."));
                }

                Serilog.Log.Information($"[OnboardingService] Sign-Up successful. OwnerId: {ownerId}");

                // 2. EXTRA STEP: Force SignIn to establish session
                try
                {
                    Serilog.Log.Information("[OnboardingService] Forcing SignIn to establish session...");
                    var signInSession = await _supabase.Auth.SignIn(email, password);
                    if (signInSession != null)
                    {
                        Serilog.Log.Information("[OnboardingService] SignIn successful. Client is now authenticated.");
                    }
                    else 
                    {
                        Serilog.Log.Warning("[OnboardingService] SignIn returned null. Client might still be anon.");
                    }
                }
                catch (Exception ex)
                {
                    Serilog.Log.Warning($"[OnboardingService] SignIn failed (might be expected if confirmation is required): {ex.Message}");
                    // Proceeding anyway because we have the ownerId
                }

                // 3. Call Postgres RPC for atomic onboarding with RETRIES
                // Propagation from Auth to profiles can be slow (race condition)
                Guid? tenantId = null;
                int maxRetries = 5;
                
                Serilog.Log.Information("[OnboardingService] Waiting 5s for initial Auth/DB propagation...");
                await Task.Delay(5000);

                for (int i = 0; i < maxRetries; i++)
                {
                    try 
                    {
                        Serilog.Log.Information($"[OnboardingService] RPC Attempt {i+1}/{maxRetries} for license: {licenseKey}");
                        var parameters = new Dictionary<string, object>
                        {
                            { "p_license_key", licenseKey },
                            { "p_owner_id", ownerId.Value },
                            { "p_tenant_name", tenantName },
                            { "p_tenant_slug", tenantSlug + "-" + DateTime.UtcNow.Ticks.ToString().Substring(10) }
                        };

                        var rpcTask = _supabase.Rpc("onboard_new_tenant", parameters);
                        var rpcResponse = await rpcTask.WaitAsync(TimeSpan.FromSeconds(NetworkTimeoutSeconds));
                        
                        if (!string.IsNullOrEmpty(rpcResponse.Content) && rpcResponse.Content != "null")
                        {
                            tenantId = Guid.Parse(rpcResponse.Content.Trim('"'));
                            break; 
                        }
                    }
                    catch (Supabase.Postgrest.Exceptions.PostgrestException ex) 
                    {
                        if (ex.Message.Contains("23503") || ex.Message.Contains("violates foreign key constraint"))
                        {
                            Serilog.Log.Warning($"[OnboardingService] Race condition (FK). Retry {i+1}...");
                        }
                        else if (ex.Message.Contains("P0001") || ex.Message.Contains("already assigned"))
                        {
                            Serilog.Log.Error("[OnboardingService] License already assigned (P0001).");
                            return Result.Failure<Guid>(new Error("Onboarding.LicenseAssigned", "This license key is already linked to another organization. Please contact support or use a different key."));
                        }
                        else 
                        {
                            throw; // Bubbles up to outer catch
                        }
                    }

                    await Task.Delay(1500 * (i + 1));
                }

                if (!tenantId.HasValue)
                {
                    return Result.Failure<Guid>(new Error("Onboarding.RpcFailed", "Database setup failed. Please try again in a few moments."));
                }

                return Result.Success(tenantId.Value);
            }
            catch (TimeoutException)
            {
                return Result.Failure<Guid>(new Error("Onboarding.NetworkTimeout", "Connection timed out. Check your internet."));
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, $"[OnboardingService] Final failure: {ex.Message}");
                return Result.Failure<Guid>(new Error("Onboarding.Error", $"Onboarding failed: {ex.Message}"));
            }
        }

        public async Task<Result> RegisterCurrentDeviceAsync(Guid tenantId, string label)
        {
            try
            {
                var hardwareId = _tenantService.GetHardwareId();
                var device = new TenantDeviceModel
                {
                    TenantId = tenantId,
                    HardwareId = hardwareId,
                    Label = label
                };

                var insertTask = _supabase.From<TenantDeviceModel>().Insert(device);
                await insertTask.WaitAsync(TimeSpan.FromSeconds(NetworkTimeoutSeconds));
                
                return Result.Success();
            }
            catch (TimeoutException)
            {
                return Result.Failure(new Error("Onboarding.NetworkTimeout", "Connection timed out. Please check your internet connection."));
            }
            catch (Exception ex)
            {
                if (ex.Message.Contains("Device limit reached"))
                {
                    return Result.Failure(new Error("Onboarding.DeviceLimit", "This tenant has reached the maximum number of allowed devices (3)."));
                }
                return Result.Failure(new Error("Onboarding.DeviceError", $"Device registration failed: {ex.Message}"));
            }
        }

        // --- MODELS FOR SUPABASE MAPPING (INTERNAL) ---
        [Table("licenses")]
        private class LicenseModel : BaseModel
        {
            [Column("license_key")] public string LicenseKey { get; set; } = null!;
            [Column("tenant_id")] public Guid? TenantId { get; set; }
        }

        [Table("tenant_devices")]
        public class TenantDeviceModel : BaseModel
        {
            [Column("tenant_id")] public Guid TenantId { get; set; }
            [Column("hardware_id")] public string HardwareId { get; set; } = null!;
            [Column("label")] public string Label { get; set; } = null!;
        }
    }
}
