using System.Threading.Tasks;
using Management.Domain.DTOs;
using Management.Domain.Primitives;

namespace Management.Domain.Services
{
    /// <summary>
    /// Defines the contract for user authentication and session management.
    /// Implemented in Infrastructure using a specific provider (e.g. Supabase, Auth0, SQL).
    /// </summary>
    public interface IAuthenticationService
    {
        /// <summary>
        /// Attempts to authenticate a user with credentials.
        /// </summary>
        /// <param name="email">Staff email address.</param>
        /// <param name="password">User password.</param>
        /// <returns>The authenticated Staff Profile (DTO) if successful.</returns>
        Task<Result<StaffDto>> LoginAsync(string email, string password);

        /// <summary>
        /// Signs the current user out and clears local session data.
        /// </summary>
        Task<Result> LogoutAsync();

        /// <summary>
        /// Checks if a valid session exists (e.g. on App Startup) and retrieves the profile.
        /// </summary>
        /// <returns>The Staff Profile if logged in, otherwise null.</returns>
        Task<Result<StaffDto>> GetCurrentUserAsync();

        /// <summary>
        /// Refreshes the current session tokens.
        /// </summary>
        /// <returns>Success if refreshed, Failure otherwise.</returns>
        Task<Result> RefreshSessionAsync();
    }
}