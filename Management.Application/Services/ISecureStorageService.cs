using System.Threading.Tasks;

namespace Management.Application.Services
{
    /// <summary>
    /// Service for secure storage of sensitive data (e.g., encryption keys, tokens).
    /// </summary>
    public interface ISecureStorageService
    {
        /// <summary>
        /// Retrieves a value from secure storage.
        /// </summary>
        Task<string?> GetAsync(string key);

        /// <summary>
        /// Stores a value in secure storage.
        /// </summary>
        Task SetAsync(string key, string value);

        /// <summary>
        /// Removes a value from secure storage.
        /// </summary>
        Task RemoveAsync(string key);

        /// <summary>
        /// Checks if a key exists in secure storage.
        /// </summary>
        Task<bool> ContainsKeyAsync(string key);

        /// <summary>
        /// Clears all secure storage (use with caution).
        /// </summary>
        Task ClearAsync();
    }
}
