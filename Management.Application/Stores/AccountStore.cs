using Management.Application.DTOs;
using System;
using System.Linq;

using Management.Domain.Interfaces;

namespace Management.Application.Stores
{
    /// <summary>
    /// Holds the state of the currently authenticated user session.
    /// Registered as a Singleton to persist across navigation.
    /// </summary>
    public class AccountStore : IStateResettable
    {
        public void ResetState()
        {
            // Facility switch doesn't mean logout, but we ensure 
            // the context is clean if needed. 
            // In some cases, we might want to Logout if the user 
            // doesn't have permissions for the new facility.
        }
        private readonly Domain.Services.ITenantService _tenantService;

        public AccountStore(Domain.Services.ITenantService tenantService)
        {
            _tenantService = tenantService;
        }

        // Event raised when the user logs in, logs out, or profile updates
        public event Action? CurrentAccountChanged;

        private StaffDto? _currentAccount;

        /// <summary>
        /// The profile of the currently logged-in staff member.
        /// Null if no user is authenticated.
        /// </summary>
        public StaffDto? CurrentAccount
        {
            get => _currentAccount;
            private set
            {
                _currentAccount = value;
                OnCurrentAccountChanged();
            }
        }

        /// <summary>
        /// Helper to check if a valid session exists.
        /// </summary>
        public bool IsLoggedIn => CurrentAccount != null;

        /// <summary>
        /// Sets the current user context.
        /// </summary>
        public void Login(StaffDto account)
        {
            CurrentAccount = account;
            if (account != null)
            {
                _tenantService.SetTenantId(account.TenantId);
            }
        }

        /// <summary>
        /// Clears the current user context.
        /// </summary>
        public void Logout()
        {
            CurrentAccount = null;
            _tenantService.Clear();
        }

        /// <summary>
        /// Checks if the current user has a specific permission.
        /// Returns false if logged out.
        /// </summary>
        /// <param name="permissionName">The name/code of the permission to check.</param>
        public bool HasPermission(string permissionName)
        {
            if (!IsLoggedIn) return false;

            // Assuming StaffDto has a List<PermissionDto> as defined in Domain layer
            return CurrentAccount?.Permissions != null &&
                   CurrentAccount.Permissions.Any(p => p.Name.Equals(permissionName, StringComparison.OrdinalIgnoreCase) && p.IsGranted);
        }

        private void OnCurrentAccountChanged()
        {
            CurrentAccountChanged?.Invoke();
        }
    }
}
