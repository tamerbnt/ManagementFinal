using Management.Domain.DTOs;
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

namespace Management.Infrastructure.Services
{
    public class AuthenticationService : IAuthenticationService
    {
        private readonly Supabase.Client _supabase;
        private readonly IStaffRepository _staffRepository;

        // Simple in-memory cache for the current session context
        private StaffDto? _currentUser;

        private readonly ISessionStorageService _sessionStorage;

        public AuthenticationService(
            Supabase.Client supabase,
            IStaffRepository staffRepository,
            ISessionStorageService sessionStorage)
        {
            _supabase = supabase;
            _staffRepository = staffRepository;
            _sessionStorage = sessionStorage;
        }

        public async Task<Result<StaffDto>> LoginAsync(string email, string password)
        {
            try
            {
                // 1. Authenticate with Supabase (Cloud)
                var session = await ResiliencePolicyRegistry.CloudRetryPolicy.ExecuteAsync(() => 
                    _supabase.Auth.SignIn(email, password));

                if (session?.User == null || string.IsNullOrEmpty(session.User.Email))
                {
                    return Result.Failure<StaffDto>(new Error("Auth.InvalidCredentials", "Invalid credentials."));
                }

                // 2. Validate against Local Database (Infrastructure)
                var staffEntity = await _staffRepository.GetByEmailAsync(session.User.Email);

                if (staffEntity == null)
                {
                    await ResiliencePolicyRegistry.CloudRetryPolicy.ExecuteAsync(() => _supabase.Auth.SignOut());
                    return Result.Failure<StaffDto>(new Error("Auth.NoProfile", "User is authenticated but has no staff profile."));
                }

                if (!staffEntity.IsActive)
                {
                    await ResiliencePolicyRegistry.CloudRetryPolicy.ExecuteAsync(() => _supabase.Auth.SignOut());
                    return Result.Failure<StaffDto>(new Error("Auth.Inactive", "Account has been deactivated."));
                }

                // 3. Persist Session (Infrastructure)
                var sessionData = new Domain.Models.SessionData
                {
                    AccessToken = session.AccessToken ?? string.Empty,
                    RefreshToken = session.RefreshToken ?? string.Empty,
                    ExpiresAt = session.ExpiresAt(),
                    StaffId = staffEntity.Id,
                    Email = staffEntity.Email.Value,
                    FullName = staffEntity.FullName,
                    Role = staffEntity.Role.ToString()
                };

                await _sessionStorage.SaveSessionAsync(sessionData);

                // 4. Map to DTO and Cache
                _currentUser = MapToDto(staffEntity);
                return Result.Success(_currentUser);
            }
            catch (Exception ex)
            {
                return Result.Failure<StaffDto>(new Error("Auth.Error", $"Login failed: {ex.Message}"));
            }
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
                        // Re-hydrate Supabase Client (if supported, else we might need to manually set tokens)
                        // This part depends on Supabase C# lib capabilities. 
                        // For now we assume we just validate against DB using the stored ID/Email.
                        // Ideally we should do: await _supabase.Auth.SetSession(storedSession.AccessToken, storedSession.RefreshToken);
                        // But Gotrue-csharp might not expose this easily without re-authenticating.
                        
                        // For now, let's trust the stored session and validate against DB
                    } 
                    catch { /* Ignore, proceed to DB check */ }
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
            if (string.IsNullOrEmpty(email)) return Result.Failure<StaffDto>(new Error("Auth.InvalidSession", "Invalid session data."));

            var staffEntity = await _staffRepository.GetByEmailAsync(email);
            if (staffEntity == null || !staffEntity.IsActive) return Result.Failure<StaffDto>(new Error("Auth.ProfileMissing", "Profile missing or inactive."));

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

        // --- Helper: Entity to DTO Mapper ---
        private StaffDto MapToDto(Domain.Models.StaffMember entity)
        {
            return new StaffDto
            {
                Id = entity.Id,
                TenantId = entity.TenantId,
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

            if (role >= StaffRole.Trainer)
            {
                perms.Add(new PermissionDto("View Members", true));
                perms.Add(new PermissionDto("Check-In", true));
            }

            if (role >= StaffRole.Manager)
            {
                perms.Add(new PermissionDto("Manage Members", true));
                perms.Add(new PermissionDto("View Finance", true));
                perms.Add(new PermissionDto("Manage Inventory", true));
            }

            if (role == StaffRole.Admin)
            {
                perms.Add(new PermissionDto("System Settings", true));
                perms.Add(new PermissionDto("Manage Staff", true));
                perms.Add(new PermissionDto("Hardware Config", true));
            }

            return perms;
        }
    }
}