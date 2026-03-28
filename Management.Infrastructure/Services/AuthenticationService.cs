using Management.Application.DTOs;
using BCryptNet = BCrypt.Net.BCrypt;
using Management.Application.Services;
using Management.Domain.Enums;
using Management.Domain.Exceptions;
using Management.Domain.Services;
using Management.Domain.Interfaces;
using Management.Infrastructure.Configuration;
using Management.Domain.Primitives;
using Supabase.Gotrue; // Required for Session handling
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Management.Infrastructure.Integrations.Supabase.Models;
using Management.Domain.Models;
using Management.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace Management.Infrastructure.Services
{
    public class AuthenticationService : IAuthenticationService
    {
        private readonly Supabase.Client _supabase;
        private readonly IStaffRepository _staffRepository;

        // Simple in-memory cache for the current session context
        private StaffDto? _currentUser;

        private readonly IFacilityContextService _facilityContext;
        private readonly ITenantService _tenantService;
        private readonly Management.Domain.Services.ISessionStorageService _sessionStorage;
        private readonly IOnboardingService _onboardingService;

        public AuthenticationService(
            Supabase.Client supabase,
            IStaffRepository staffRepository,
            Management.Domain.Services.ISessionStorageService sessionStorage,
            IFacilityContextService facilityContext,
            ITenantService tenantService,
            IOnboardingService onboardingService)
        {
            _supabase = supabase;
            _staffRepository = staffRepository;
            _sessionStorage = sessionStorage;
            _facilityContext = facilityContext;
            _tenantService = tenantService;
            _onboardingService = onboardingService;
        }

        public async Task<Result<StaffDto>> LoginAsync(string email, string password, Guid? facilityId = null, Management.Domain.Enums.FacilityType? targetType = null)
        {
            email = email?.Trim().ToLowerInvariant() ?? string.Empty;
            try
            {
                if (string.IsNullOrWhiteSpace(email))
                    return Result.Failure<StaffDto>(new Error("Auth.InvalidInput", "Email is required."));

                Serilog.Log.Information($"[AuthService] Attempting login for {email}. Context: {facilityId}, TargetType: {targetType}");
                
                // 1. Authenticate with Supabase (Cloud)
                var session = await ResiliencePolicyRegistry.CloudRetryPolicy.ExecuteAsync(() => 
                    _supabase.Auth.SignIn(email, password));

                if (session?.User == null || string.IsNullOrEmpty(session.User.Email))
                {
                    return Result.Failure<StaffDto>(new Error("Auth.InvalidCredentials", "Invalid credentials."));
                }

                // 2. Resolve Staff Profile (Local or Cloud Recovery)
                var resolveResult = await ResolveStaffProfileAsync(email, facilityId, targetType);
                if (resolveResult.IsFailure)
                {
                    await ResiliencePolicyRegistry.CloudRetryPolicy.ExecuteAsync(() => _supabase.Auth.SignOut());
                    return Result.Failure<StaffDto>(resolveResult.Error);
                }

                var staffEntity = resolveResult.Value;

                // SECURITY FIX 2: Explicit facility matching for online login.
                // Ensures staff members can only access their assigned facility.
                // Owners are exempted as they manage the entire tenant.
                if (facilityId.HasValue 
                    && facilityId.Value != Guid.Empty 
                    && staffEntity.FacilityId != facilityId.Value 
                    && staffEntity.Role != Management.Domain.Enums.StaffRole.Owner)
                {
                    await ResiliencePolicyRegistry.CloudRetryPolicy.ExecuteAsync(() => _supabase.Auth.SignOut());
                    Serilog.Log.Warning("[Security] Facility mismatch during login. Staff={StaffId} StaffFacility={StaffFacility} RequestedFacility={RequestedFacility}",
                        staffEntity.Id, staffEntity.FacilityId, facilityId.Value);
                    
                    return Result.Failure<StaffDto>(new Error(
                        "Auth.FacilityMismatch", 
                        "Access denied: Your account is not authorized for this facility."));
                }

                if (!staffEntity.IsActive)
                {
                    await ResiliencePolicyRegistry.CloudRetryPolicy.ExecuteAsync(() => _supabase.Auth.SignOut());
                    return Result.Failure<StaffDto>(new Error("Auth.Inactive", "Account has been deactivated."));
                }

                // 3. Update Global Context & Cloud Metadata
                await UpdateContextAndMetadataAsync(staffEntity);

                // 4. Persist Session (Infrastructure)
                var finalSession = _supabase.Auth.CurrentSession ?? session;
                await PersistSessionDataAsync(staffEntity, finalSession);

                // 5. Map to DTO and Cache
                _currentUser = MapToDto(staffEntity);
                return Result.Success(_currentUser);
            }
            catch (Exception ex)
            {
                return await HandleLoginFailureAsync(email, password, facilityId, ex);
            }
        }

        private async Task<Result<StaffMember>> ResolveStaffProfileAsync(string email, Guid? facilityId, FacilityType? targetType)
        {
            // SECURITY FIX 3: Stricter lookup requirements.
            // If no facilityId is provided, this is a misconfigured PC (unless it's the Owner onboarding path).
            if (!facilityId.HasValue || facilityId.Value == Guid.Empty)
            {
                if (!targetType.HasValue)
                {
                    Serilog.Log.Warning("[Security] ResolveStaffProfileAsync called with null facilityId and no targetType for {Email}. Blocking.", email);
                    return Result.Failure<StaffMember>(new Error(
                        "Auth.FacilityNotConfigured",
                        "This PC is not configured for any facility. Please complete facility setup first."));
                }
            }

            // 1. Local Lookup
            StaffMember? staffEntity = null;
            if (facilityId.HasValue && facilityId.Value != Guid.Empty)
            {
                staffEntity = await _staffRepository.GetByEmailAsync(email, facilityId.Value);
                if (staffEntity != null && staffEntity.FacilityId != facilityId.Value)
                {
                    Serilog.Log.Information($"[AuthService] Local profile for {email} belongs to different facility {staffEntity.FacilityId}. Forcing cloud recovery.");
                    staffEntity = null;
                }
            }
            else if (targetType.HasValue)
            {
                staffEntity = await _staffRepository.GetByEmailAndFacilityTypeAsync(email, targetType.Value);
            }
            else
            {
                staffEntity = await _staffRepository.GetByEmailAsync(email, facilityId);
            }

            if (staffEntity != null) return Result.Success(staffEntity);

            // 2. Cloud Recovery (RPC)
            Serilog.Log.Information($"[AuthService] Attempting Cloud Recovery for {email}");
            var rpcResponse = await _supabase.Rpc("get_staff_profiles", new Dictionary<string, object> { { "p_email", email } });
            
            if (rpcResponse == null || string.IsNullOrEmpty(rpcResponse.Content) || rpcResponse.Content == "null")
            {
                return Result.Failure<StaffMember>(new Error("Auth.NoProfile", "You are not authorized for this facility context."));
            }

            var _snakeCaseSettings = new Newtonsoft.Json.JsonSerializerSettings
            {
                ContractResolver = new Newtonsoft.Json.Serialization.DefaultContractResolver { NamingStrategy = new Newtonsoft.Json.Serialization.SnakeCaseNamingStrategy() }
            };

            var remoteProfiles = Newtonsoft.Json.JsonConvert.DeserializeObject<List<SupabaseStaffMember>>(rpcResponse.Content, _snakeCaseSettings);
            if (remoteProfiles == null || !remoteProfiles.Any())
            {
                return Result.Failure<StaffMember>(new Error("Auth.NoProfile", "No profiles found."));
            }

            // Seed discovery
            var firstProfile = remoteProfiles.First();
            _tenantService.SetTenantId(firstProfile.TenantId);
            await SeedLocalFacilitiesFromSupabaseAsync(firstProfile.TenantId);

            // Match logic
            SupabaseStaffMember? validRemoteProfile = null;
            if (facilityId.HasValue && facilityId.Value != Guid.Empty)
            {
                validRemoteProfile = remoteProfiles.FirstOrDefault(p => p.FacilityId == facilityId.Value);
            }
            else if (targetType.HasValue)
            {
                foreach (var profile in remoteProfiles)
                {
                    var localFacilityType = await _staffRepository.GetFacilityTypeByIdAsync(profile.FacilityId);
                    if (localFacilityType.HasValue && localFacilityType.Value == targetType.Value)
                    {
                        validRemoteProfile = profile;
                        break;
                    }
                }
            }
            else
            {
                validRemoteProfile = remoteProfiles.FirstOrDefault();
            }

            if (validRemoteProfile != null)
            {
                staffEntity = MapSupabaseToDomain(validRemoteProfile);
                await _staffRepository.SafeAddAsync(staffEntity);
                return Result.Success(staffEntity);
            }

            // 3. On-Demand Provisioning (Owner Only)
            if (targetType.HasValue && !facilityId.HasValue)
            {
                return await HandleOwnerOnboardingAsync(email, remoteProfiles, targetType.Value);
            }

            return Result.Failure<StaffMember>(new Error("Auth.NoProfile", "Access Denied: No valid profile found for this context."));
        }

        private async Task<Result<StaffMember>> HandleOwnerOnboardingAsync(string email, List<SupabaseStaffMember> remoteProfiles, FacilityType targetType)
        {
            var ownerProfile = remoteProfiles.FirstOrDefault(m => m.Role == (int)StaffRole.Owner);
            if (ownerProfile == null) return Result.Failure<StaffMember>(new Error("Auth.NoOwnerProfile", "On-demand provisioning requires Owner permissions."));

            Serilog.Log.Information($"[AuthService] Owner {email} verified. Triggering on-demand provisioning for {targetType}...");
            
            var provisionResult = await _onboardingService.ProvisionFacilityAsync(
                ownerProfile.TenantId,
                ownerProfile.Id,
                email,
                ownerProfile.FullName ?? "Owner",
                (int)targetType,
                $"{ownerProfile.FullName}'s {targetType}"
            );

            if (!provisionResult.IsSuccess) return Result.Failure<StaffMember>(provisionResult.Error);

            // Re-discover via RPC
            var rpcResponse = await _supabase.Rpc("get_staff_profiles", new Dictionary<string, object> { { "p_email", email } });
            if (rpcResponse != null && !string.IsNullOrEmpty(rpcResponse.Content) && rpcResponse.Content != "null")
            {
                 var _snakeCaseSettings = new Newtonsoft.Json.JsonSerializerSettings
                {
                    ContractResolver = new Newtonsoft.Json.Serialization.DefaultContractResolver { NamingStrategy = new Newtonsoft.Json.Serialization.SnakeCaseNamingStrategy() }
                };
                var updatedProfiles = Newtonsoft.Json.JsonConvert.DeserializeObject<List<SupabaseStaffMember>>(rpcResponse.Content, _snakeCaseSettings);
                var newProfile = updatedProfiles?.FirstOrDefault(p => p.FacilityId == provisionResult.Value);
                
                if (newProfile != null)
                {
                    var staffEntity = MapSupabaseToDomain(newProfile);
                    await _staffRepository.SafeAddAsync(staffEntity);
                    _facilityContext.UpdateFacilityId(targetType, provisionResult.Value);
                    return Result.Success(staffEntity);
                }
            }

            return Result.Failure<StaffMember>(new Error("Auth.ProvisioningSuccessSyncFail", "Facility created but profile sync failed."));
        }

        private async Task UpdateContextAndMetadataAsync(StaffMember staffEntity)
        {
            _tenantService.SetTenantId(staffEntity.TenantId);
            _tenantService.SetUserId(staffEntity.Id);
            _tenantService.SetRole(staffEntity.Role.ToString());

            // --- Phase 6 HEALING: Propagate Facility Context ---
            // Ensures local data scoped by FacilityId (Members, Products) becomes visible in the UI.
            try
            {
                var type = await _staffRepository.GetFacilityTypeByIdAsync(staffEntity.FacilityId);
                if (type.HasValue)
                {
                    _facilityContext.UpdateFacilityId(type.Value, staffEntity.FacilityId);
                }
            }
            catch (Exception ex)
            {
                Serilog.Log.Warning(ex, "[AuthService] Local facility discovery failed during context hydration.");
            }

            if (_supabase.Auth.CurrentSession != null)
            {
                try
                {
                    var metadata = new Dictionary<string, object>
                    {
                        { "tenant_id", staffEntity.TenantId },
                        { "facility_id", staffEntity.FacilityId },
                        { "role", staffEntity.Role.ToString() }
                    };
                    await _supabase.Auth.Update(new UserAttributes { Data = metadata });
                    await _supabase.Auth.RefreshSession();
                }
                catch (Exception ex)
                {
                    Serilog.Log.Warning($"[AuthService] Failed to update user metadata for RLS: {ex.Message}");
                }
            }
        }

        private async Task PersistSessionDataAsync(StaffMember staffEntity, Session session)
        {
            Serilog.Log.Information($"[AuthService] Persisting session. Token starts with: {session.AccessToken?.Substring(0, 10)}...");

            var sessionData = new Domain.Models.SessionData
            {
                AccessToken = session.AccessToken ?? string.Empty,
                RefreshToken = session.RefreshToken ?? string.Empty,
                ExpiresAt = session.ExpiresAt(),
                StaffId = staffEntity.Id,
                FacilityId = staffEntity.FacilityId,
                Email = staffEntity.Email.Value,
                FullName = staffEntity.FullName,
                Role = staffEntity.Role.ToString()
            };

            await _sessionStorage.SaveSessionAsync(sessionData);
        }

        private async Task<Result<StaffDto>> HandleLoginFailureAsync(string email, string password, Guid? facilityId, Exception ex)
        {
            // OFFLINE FALLBACK
            try 
            {
                var localStaff = await _staffRepository.GetByEmailAsync(email, facilityId);
                if (localStaff != null && localStaff.IsActive)
                {
                    bool isValid = false;
                    bool needsRehash = false;

                    // Try BCrypt verification first
                    try
                    {
                        if (BCryptNet.Verify(password, localStaff.PinCode))
                        {
                            isValid = true;
                        }
                    }
                    catch
                    {
                        // Not a BCrypt hash, try plain text comparison for legacy support
                        if (localStaff.PinCode == password)
                        {
                            isValid = true;
                            needsRehash = true;
                        }
                    }

                    if (isValid)
                    {
                        if (needsRehash)
                        {
                            try
                            {
                                // Auto-migrate to BCrypt
                                string hashedPin = BCryptNet.HashPassword(password);
                                // We need a way to update the PIN. Assuming a method or direct access.
                                // localStaff.SetPinCode(hashedPin) if available, or repository update.
                                // For now, we'll try to update via repository if possible.
                                await _staffRepository.UpdatePinAsync(localStaff.Id, hashedPin);
                                Serilog.Log.Information("Staff {Email} PIN auto-migrated to BCrypt.", email);
                            }
                            catch (Exception rehashEx)
                            {
                                Serilog.Log.Warning(rehashEx, "Failed to auto-migrate PIN for staff {Email}", email);
                            }
                        }

                        if (facilityId.HasValue && localStaff.FacilityId != facilityId.Value)
                        {
                            return Result.Failure<StaffDto>(new Error("Auth.FacilityMismatch", "Incorrect facility selected."));
                        }

                        var offlineSession = new Domain.Models.SessionData
                        {
                            AccessToken = "OFFLINE_ACCESS_TOKEN", 
                            RefreshToken = "OFFLINE_REFRESH_TOKEN",
                            ExpiresAt = DateTime.UtcNow.AddHours(12),
                            StaffId = localStaff.Id,
                            FacilityId = localStaff.FacilityId,
                            Email = localStaff.Email.Value,
                            FullName = localStaff.FullName,
                            Role = localStaff.Role.ToString()
                        };

                        await _sessionStorage.SaveSessionAsync(offlineSession);
                        _currentUser = MapToDto(localStaff);
                        return Result.Success(_currentUser);
                    }
                }
            }
            catch { }

            var errorMsg = $"Login failed: {ex.Message}";
            if (!ex.Message.Contains("email_not_confirmed")) Serilog.Log.Error(ex, $"[AuthenticationService] {errorMsg}");
            return Result.Failure<StaffDto>(new Error("Auth.Error", errorMsg));
        }

        public async Task<Result> LogoutAsync()
        {
            await ResiliencePolicyRegistry.CloudRetryPolicy.ExecuteAsync(() => _supabase.Auth.SignOut());
            await _sessionStorage.ClearSessionAsync();
            _currentUser = null;
            return Result.Success();
        }

        public async Task<Result<StaffDto>> GetCurrentUserAsync()
        {
            // 1. Return cached user if available
            if (_currentUser != null) return Result.Success(_currentUser);

            // 2. Check if Supabase has a persisted session on disk (OR load from our custom storage)
            // Note: Supabase client might handle its own storage, but we are enforcing ours.
            // If Supabase client is empty, try loading from our encrypted file
            if (_supabase.Auth.CurrentSession == null)
            {
                var storedSession = await _sessionStorage.LoadSessionAsync();
                if (storedSession != null && !storedSession.IsExpired)
                {
                    try 
                    {
                        // Check if this is a real Supabase session (not offline mode)
                        if (storedSession.AccessToken != "OFFLINE_ACCESS_TOKEN" && 
                            storedSession.RefreshToken != "OFFLINE_REFRESH_TOKEN" &&
                            !string.IsNullOrEmpty(storedSession.AccessToken) &&
                            !string.IsNullOrEmpty(storedSession.RefreshToken))
                        {
                            // Try to restore Supabase session
                            // Note: Supabase C# client may auto-restore from its own storage,
                            // but we attempt to trigger restoration here if needed
                            // The client's AutoRefreshToken should handle this automatically
                            Serilog.Log.Information($"[AuthService] Attempting to restore Supabase session for {storedSession.Email}");
                            
                            // The Supabase client should automatically restore sessions if AutoRefreshToken is enabled
                            // If restoration fails, we'll fall back to DB validation below
                        }
                    } 
                    catch (Exception restoreEx) 
                    { 
                        Serilog.Log.Warning(restoreEx, "[AuthService] Failed to restore Supabase session, proceeding with DB validation");
                    }
                }
            }

            var session = _supabase.Auth.CurrentSession;

            // If still no session in Supabase, try to fallback to our Email check
            // But realistically, if Supabase isn't happy, we shouldn't be either.
            // However, we are storing session independently. 
            
            // Let's rely on the simple check: Do we have a valid stored session?
            var mySession = await _sessionStorage.LoadSessionAsync();

            if (mySession == null || mySession.IsExpired)
            {
                return Result.Failure<StaffDto>(new Error("Auth.NoSession", "No active session."));
            }

            // 3. Re-hydrate User from DB
            var email = mySession.Email;
            var facilityId = mySession.FacilityId;
            if (string.IsNullOrEmpty(email)) return Result.Failure<StaffDto>(new Error("Auth.InvalidSession", "Invalid session data."));

            var staffEntity = await _staffRepository.GetByEmailAsync(email, facilityId);
            
            // --- Phase 5 HEALING: Cloud Recovery ---
            // If local SQLite is missing the profile (race condition after PC 1 registration),
            // trigger an RPC discovery to pull the profile from Supabase and seed the local DB.
            if (staffEntity == null && _supabase.Auth.CurrentSession != null)
            {
                Serilog.Log.Information($"[AuthService] Profile missing locally for {email}. Triggering Cloud Recovery hydration...");
                
                var parameters = new Dictionary<string, object> { { "p_email", email } };
                var rpcResponse = await _supabase.Rpc("get_staff_profiles", parameters);
                
                if (rpcResponse != null && !string.IsNullOrEmpty(rpcResponse.Content) && rpcResponse.Content != "null")
                {
                    var _snakeCaseSettings = new Newtonsoft.Json.JsonSerializerSettings
                    {
                        ContractResolver = new Newtonsoft.Json.Serialization.DefaultContractResolver
                        {
                            NamingStrategy = new Newtonsoft.Json.Serialization.SnakeCaseNamingStrategy()
                        }
                    };

                    var remoteProfiles = Newtonsoft.Json.JsonConvert.DeserializeObject<List<SupabaseStaffMember>>(rpcResponse.Content, _snakeCaseSettings);
                    var validRemoteProfile = remoteProfiles?.FirstOrDefault(p => p.FacilityId == facilityId) ?? remoteProfiles?.FirstOrDefault();

                    if (validRemoteProfile != null)
                    {
                        staffEntity = MapSupabaseToDomain(validRemoteProfile);
                        await _staffRepository.SafeAddAsync(staffEntity);
                        Serilog.Log.Information($"[AuthService] Cloud Recovery successful. Re-seeded profile for {email}.");
                    }
                }
            }

            if (staffEntity == null || !staffEntity.IsActive) return Result.Failure<StaffDto>(new Error("Auth.ProfileMissing", "Profile missing or inactive."));

            // Re-hydrate application context for restored session (Critical for Facility ID scoping)
            await UpdateContextAndMetadataAsync(staffEntity);

            _currentUser = MapToDto(staffEntity);
            return Result.Success(_currentUser);
        }

        public async Task<Result> RefreshSessionAsync()
        {
            try
            {
                var session = _supabase.Auth.CurrentSession;
                if (session == null || string.IsNullOrEmpty(session.RefreshToken))
                {
                    return Result.Failure(new Error("Auth.NoSession", "No active session to refresh."));
                }

                await _supabase.Auth.RefreshSession();
                
                // Ensure user profile is still valid
                var currentUserResult = await GetCurrentUserAsync();
                if (currentUserResult.IsFailure)
                {
                    return Result.Failure(currentUserResult.Error);
                }

                // Update persisted session
                // We need to fetch the LATEST session from Supabase client after refresh
                var newSession = _supabase.Auth.CurrentSession;
                if (newSession != null && _currentUser != null)
                {
                     var sessionData = new Domain.Models.SessionData
                    {
                        AccessToken = newSession.AccessToken ?? string.Empty,
                        RefreshToken = newSession.RefreshToken ?? string.Empty,
                        ExpiresAt = newSession.ExpiresAt(),
                        StaffId = _currentUser.Id,
                        Email = _currentUser.Email,
                        FullName = _currentUser.FullName,
                        Role = _currentUser.Role.ToString()
                    };
                    await _sessionStorage.SaveSessionAsync(sessionData);
                }

                return Result.Success();
            }
            catch (Exception ex)
            {
                return Result.Failure(new Error("Auth.RefreshFailed", $"Failed to refresh session: {ex.Message}"));
            }
        }

        public async Task<Result<Guid>> RegisterStaffAsync(string email, string password)
        {
            try 
            {
                // Note: SignUp typically triggers email confirmation.
                // In production, you might want to use the Admin API to auto-confirm.
                // For this implementation, we use SignUp as it's available via the regular key.
                var result = await _supabase.Auth.SignUp(email, password);
                
                if (result?.User == null || string.IsNullOrEmpty(result.User.Id))
                {
                    return Result.Failure<Guid>(new Error("Auth.RegistrationFailed", "Failed to create cloud account."));
                }

                return Result.Success(Guid.Parse(result.User.Id));
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, $"[AuthService] Registration failed for {email}");
                return Result.Failure<Guid>(new Error("Auth.RegistrationError", ex.Message));
            }
        }

        // Internal helper for rollback (Compensating Action)
        public async Task<Result> RemoveUserAsync(Guid supabaseUserId)
        {
            try 
            {
                // Note: Supabase regular API doesn't allow deleting users. 
                // This would require Service Role/Admin key.
                // For now, we log this as a manual cleanup requirement if we can't automate it.
                Serilog.Log.Warning($"[AuthService] Manual Cleanup Required: Database save failed for user {supabaseUserId}. " +
                                  $"Automatic rollback of Supabase Auth accounts requires Admin API access.");
                
                return Result.Success();
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, $"[AuthService] Rollback failed for {supabaseUserId}");
                return Result.Failure(new Error("Auth.RollbackError", ex.Message));
            }
        }

        public async Task<Dictionary<FacilityType, Guid>> EnsureTenantFacilitiesAsync(Guid tenantId, StaffRole userRole)
        {
            var facilityMap = new Dictionary<FacilityType, Guid>();
            
            try
            {
                Serilog.Log.Information($"[AuthService] Ensuring facilities exist for tenant {tenantId} (User Role: {userRole})");

                // 1. Fetch Existing
                var result = await _supabase.From<SupabaseFacility>()
                    .Filter("tenant_id", Supabase.Postgrest.Constants.Operator.Equals, tenantId.ToString())
                    .Get();

                if (result.Models.Any())
                {
                    foreach (var facility in result.Models)
                    {
                        // Map integer type to Enum
                        if (Enum.IsDefined(typeof(FacilityType), facility.Type))
                        {
                            var type = (FacilityType)facility.Type;
                            facilityMap[type] = facility.Id;
                        }
                    }
                }

                // NOTE: We NO LONGER auto-provision missing facilities here.
                // Provisioning now happens eagerly during Onboarding on PC 1.
                // This prevents duplicate facility creation on every login.
                if (!facilityMap.Any())
                {
                    Serilog.Log.Error($"[AuthService] CRITICAL FAIL-SAFE: Supabase returned 0 facilities for {tenantId}.");
                    Serilog.Log.Error("[AuthService] This is likely a network drop or RLS failure. Rejecting Empty List.");
                    Serilog.Log.Error("[AuthService] Returning an empty map but signaling FacilityContextService NOT to overwrite local JSON.");
                    return facilityMap; // FacilityContextService must catch this and refuse to wipe.
                }

                return facilityMap;
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, $"[AuthService] Failed to ensure facilities for tenant {tenantId}");
                return facilityMap; // Return what we found, fail open
            }
        }

        public async Task<bool> TenantHasOwnerAccountAsync(Guid tenantId)
        {
            try
            {
                Serilog.Log.Information($"[AuthService] Checking for existing owner account for tenant {tenantId}...");

                // Query 'profiles' table for role = 1 (Owner) and tenant_id
                // StaffRole.Owner is usually 1 (verified in StaffMember mapping)
                var result = await _supabase.From<SupabaseStaffMember>()
                    .Filter("tenant_id", Supabase.Postgrest.Constants.Operator.Equals, tenantId.ToString())
                    .Filter("role", Supabase.Postgrest.Constants.Operator.Equals, (int)StaffRole.Owner)
                    .Get();

                bool hasOwner = result.Models.Any();
                Serilog.Log.Information($"[AuthService] Existing owner check for {tenantId}: {hasOwner}");
                return hasOwner;
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, $"[AuthService] Failed to check for existing owner account for tenant {tenantId}");
                return false; // Fail safe to account creation if we can't verify balance
            }
        }

        // --- Helper: Seed Local Facilities from Supabase ---
        // Called during Cloud Recovery, AFTER Supabase authentication succeeds but BEFORE
        // local facility type validation. This populates the local SQLite Facilities table
        // so that GetFacilityTypeByIdAsync can work correctly.
        private async Task SeedLocalFacilitiesFromSupabaseAsync(Guid tenantId)
        {
            if (tenantId == Guid.Empty) return;
            try
            {
                Serilog.Log.Information($"[AuthService] Seeding local Facilities table from Supabase for tenant {tenantId} via RPC...");
                
                var parameters = new Dictionary<string, object> { { "p_tenant_id", tenantId } };
                var rpcResponse = await _supabase.Rpc("get_tenant_facilities", parameters);

                if (rpcResponse == null || string.IsNullOrEmpty(rpcResponse.Content) || rpcResponse.Content == "null")
                {
                    Serilog.Log.Warning($"[AuthService] RPC returned no facilities for tenant {tenantId}.");
                    return;
                }

                var _snakeCaseSettings = new Newtonsoft.Json.JsonSerializerSettings
                {
                    ContractResolver = new Newtonsoft.Json.Serialization.DefaultContractResolver
                    {
                        NamingStrategy = new Newtonsoft.Json.Serialization.SnakeCaseNamingStrategy()
                    }
                };

                var supaFacilities = Newtonsoft.Json.JsonConvert.DeserializeObject<List<SupabaseFacility>>(rpcResponse.Content, _snakeCaseSettings);

                if (supaFacilities == null || supaFacilities.Count == 0)
                {
                    Serilog.Log.Warning($"[AuthService] No facilities found in Supabase for tenant {tenantId}.");
                    return;
                }

                Serilog.Log.Information($"[AuthService] Found {supaFacilities.Count} facilities in Supabase. Upserting to local SQLite...");

                var appContext = (_staffRepository as Management.Infrastructure.Repositories.StaffRepository)?
                    .GetContext() as Management.Infrastructure.Data.AppDbContext;

                if (appContext == null)
                {
                    Serilog.Log.Warning("[AuthService] Cannot access AppDbContext for facility seeding. Skipping.");
                    return;
                }

                foreach (var supaFacility in supaFacilities)
                {
                    if (!System.Enum.IsDefined(typeof(FacilityType), supaFacility.Type)) continue;

                    var existingFacility = await appContext.Facilities
                        .AsNoTracking()
                        .FirstOrDefaultAsync(f => f.Id == supaFacility.Id && !f.IsDeleted);

                    if (existingFacility == null)
                    {
                        appContext.Facilities.Add(new Facility
                        {
                            Id = supaFacility.Id,
                            TenantId = supaFacility.TenantId,
                            Name = supaFacility.Name,
                            Type = (FacilityType)supaFacility.Type,
                            IsActive = supaFacility.IsActive,
                            IsSynced = true,
                        });
                    }
                    else
                    {
                        // Update core fields by loading tracked entity
                        var trackedFacility = appContext.Facilities.Local.FirstOrDefault(f => f.Id == supaFacility.Id)
                            ?? appContext.Facilities.FirstOrDefault(f => f.Id == supaFacility.Id);
                        if (trackedFacility != null)
                        {
                            trackedFacility.Type = (FacilityType)supaFacility.Type;
                            trackedFacility.Name = supaFacility.Name;
                            trackedFacility.IsActive = supaFacility.IsActive;
                        }
                    }
                }

                await appContext.SaveChangesAsync();
                Serilog.Log.Information($"[AuthService] Successfully seeded {supaFacilities.Count} facilities into local SQLite.");
            }
            catch (Exception ex)
            {
                Serilog.Log.Warning(ex, "[AuthService] Failed to seed local facilities during Cloud Recovery. Login will proceed but type validation may fail.");
            }
        }

        // --- Helper: Entity to DTO Mapper ---
        private StaffDto MapToDto(Domain.Models.StaffMember entity)
        {
            return new StaffDto
            {
                Id = entity.Id,
                TenantId = entity.TenantId,
                FacilityId = entity.FacilityId,
                FullName = entity.FullName,
                Email = entity.Email.Value,
                PhoneNumber = entity.PhoneNumber.Value,
                Role = entity.Role,
                HireDate = entity.HireDate,
                Status = entity.IsActive ? "Active" : "Inactive",
                Permissions = GeneratePermissionsForRole(entity.Role)
            };
        }

        // --- Helper: Role Based Access Control (RBAC) ---
        // Maps Enum Roles to granular Permission strings used by the UI
        private List<PermissionDto> GeneratePermissionsForRole(StaffRole role)
        {
            var perms = new List<PermissionDto>();

            // Everyone can view basic dashboards
            perms.Add(new PermissionDto("View Dashboard", true));

            // All active staff can view members and check-in
            perms.Add(new PermissionDto("View Members", true));
            perms.Add(new PermissionDto("Check-In", true));

            if (role == StaffRole.Owner)
            {
                perms.Add(new PermissionDto("Manage Members", true));
                perms.Add(new PermissionDto("View Finance", true));
                perms.Add(new PermissionDto("Manage Inventory", true));
                perms.Add(new PermissionDto("System Settings", true));
                perms.Add(new PermissionDto("Manage Staff", true));
                perms.Add(new PermissionDto("Hardware Config", true));
            }

            return perms;
        }
        private StaffMember MapSupabaseToDomain(SupabaseStaffMember remote)
        {
            var emailResult = Email.Create(remote.Email);
            
            // --- Phase 5 HEALING: Safe Email Handling ---
            // Ensure we don't crash on .Value if Supabase returns a malformed/missing email
            Email validEmail;
            if (emailResult.IsSuccess)
            {
                validEmail = emailResult.Value;
            }
            else 
            {
                // CRITICAL: Silent fallback removed to prevent "Identity Virus" propagation.
                // We log an error and throw a clear exception to stop corruption at the boundary.
                Serilog.Log.Error($"[AuthService] IDENTITIY VIRUS DETECTED: Supabase returned invalid email for user {remote.Id}. FullName contained: '{remote.FullName}'. Blocking access until data is repaired.");
                throw new InvalidOperationException($"Cloud profile for {remote.Id} is corrupt. Missing email address. (Found '{remote.FullName}' in name field). Please run the recovery script.");
            }

            var role = Enum.IsDefined(typeof(StaffRole), remote.Role) ? (StaffRole)remote.Role : StaffRole.Staff;

            var staffEntity = StaffMember.ForLocalSync(
                remote.Id,
                remote.TenantId,
                remote.FacilityId,
                remote.FullName ?? "Staff Member",
                validEmail,
                role,
                remote.IsActive,
                remote.Salary,
                remote.PaymentDay);

            if (remote.PermissionsJson != null)
            {
                try 
                {
                    var json = remote.PermissionsJson.ToString();
                    var permissions = System.Text.Json.JsonSerializer.Deserialize<System.Collections.Generic.Dictionary<string, bool>>(json);
                    if (permissions != null)
                    {
                        foreach (var p in permissions) staffEntity.SetPermission(p.Key, p.Value);
                    }
                }
                catch (Exception ex) { Serilog.Log.Warning($"[AuthService] Failed to deserialize permissions: {ex.Message}"); }
            }

            if (remote.AllowedModulesJson != null)
            {
                try 
                {
                    var json = remote.AllowedModulesJson.ToString();
                    var modules = System.Text.Json.JsonSerializer.Deserialize<System.Collections.Generic.List<string>>(json);
                    if (modules != null) staffEntity.SetAllowedModules(modules);
                }
                catch (Exception ex) { Serilog.Log.Warning($"[AuthService] Failed to deserialize allowed modules: {ex.Message}"); }
            }

            return staffEntity;
        }
    }
}
