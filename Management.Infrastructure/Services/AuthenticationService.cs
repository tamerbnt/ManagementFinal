using Management.Domain.DTOs;
using Management.Domain.Enums;
using Management.Domain.Exceptions;
using Management.Domain.Services;
using Management.Domain.Interfaces;
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
        private StaffDto _currentUser;

        public AuthenticationService(
            Supabase.Client supabase,
            IStaffRepository staffRepository)
        {
            _supabase = supabase;
            _staffRepository = staffRepository;
        }

        public async Task<StaffDto> LoginAsync(string email, string password)
        {
            try
            {
                // 1. Authenticate with Supabase (Cloud)
                var session = await _supabase.Auth.SignIn(email, password);

                if (session?.User == null || string.IsNullOrEmpty(session.User.Email))
                {
                    throw new ValidationException(new Dictionary<string, string[]>
                    {
                        { "Auth", new[] { "Invalid credentials." } }
                    });
                }

                // 2. Validate against Local Database (Infrastructure)
                // We trust Supabase for the password, but we trust SQL for the Role/Status.
                var staffEntity = await _staffRepository.GetByEmailAsync(session.User.Email);

                if (staffEntity == null)
                {
                    // User exists in Auth provider but not in Gym DB (Data integrity issue)
                    await _supabase.Auth.SignOut();
                    throw new BusinessRuleViolationException("User is authenticated but has no staff profile.");
                }

                if (!staffEntity.IsActive)
                {
                    await _supabase.Auth.SignOut();
                    throw new BusinessRuleViolationException("Account has been deactivated.");
                }

                // 3. Map to DTO and Cache
                _currentUser = MapToDto(staffEntity);
                return _currentUser;
            }
            catch (Exception ex) when (ex is not DomainException)
            {
                // Wrap raw Supabase/Network exceptions
                // Log ex.Message here in a real app
                throw new ValidationException(new Dictionary<string, string[]>
                {
                    { "Auth", new[] { "Login failed. Please check your connection and credentials." } }
                });
            }
        }

        public async Task LogoutAsync()
        {
            await _supabase.Auth.SignOut();
            _currentUser = null;
        }

        public async Task<StaffDto> GetCurrentUserAsync()
        {
            // 1. Return cached user if available
            if (_currentUser != null) return _currentUser;

            // 2. Check if Supabase has a persisted session on disk
            var session = _supabase.Auth.CurrentSession;

            // FIX: Added () to ExpiresAt because it is a Method in the library
            if (session == null || session.ExpiresAt() < DateTime.UtcNow)
            {
                return null;
            }

            // 3. Re-hydrate User from DB
            // We fetch fresh data to ensure role/permissions haven't changed since last run
            var email = session.User?.Email;
            if (string.IsNullOrEmpty(email)) return null;

            var staffEntity = await _staffRepository.GetByEmailAsync(email);
            if (staffEntity == null || !staffEntity.IsActive) return null;

            _currentUser = MapToDto(staffEntity);
            return _currentUser;
        }

        // --- Helper: Entity to DTO Mapper ---
        private StaffDto MapToDto(Domain.Models.StaffMember entity)
        {
            return new StaffDto
            {
                Id = entity.Id,
                FullName = entity.FullName,
                Email = entity.Email,
                PhoneNumber = entity.PhoneNumber,
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
            perms.Add(new PermissionDto { Name = "View Dashboard", IsGranted = true });

            if (role >= StaffRole.Trainer)
            {
                perms.Add(new PermissionDto { Name = "View Members", IsGranted = true });
                perms.Add(new PermissionDto { Name = "Check-In", IsGranted = true });
            }

            if (role >= StaffRole.Manager)
            {
                perms.Add(new PermissionDto { Name = "Manage Members", IsGranted = true });
                perms.Add(new PermissionDto { Name = "View Finance", IsGranted = true });
                perms.Add(new PermissionDto { Name = "Manage Inventory", IsGranted = true });
            }

            if (role == StaffRole.Admin)
            {
                perms.Add(new PermissionDto { Name = "System Settings", IsGranted = true });
                perms.Add(new PermissionDto { Name = "Manage Staff", IsGranted = true });
                perms.Add(new PermissionDto { Name = "Hardware Config", IsGranted = true });
            }

            return perms;
        }
    }
}