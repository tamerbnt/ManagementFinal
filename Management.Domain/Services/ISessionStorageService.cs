using Management.Domain.Models;
using System.Threading.Tasks;

namespace Management.Domain.Services
{
    /// <summary>
    /// Service for storing and retrieving user session data.
    /// </summary>
    public interface ISessionStorageService
    {
        /// <summary>
        /// Saves the session data to secure storage.
        /// </summary>
        Task SaveSessionAsync(SessionData session);

        /// <summary>
        /// Loads the session data from storage.
        /// </summary>
        Task<SessionData?> LoadSessionAsync();

        /// <summary>
        /// Clears the stored session data.
        /// </summary>
        Task ClearSessionAsync();

        /// <summary>
        /// Checks if a valid session exists.
        /// </summary>
        Task<bool> HasValidSessionAsync();
    }
}
