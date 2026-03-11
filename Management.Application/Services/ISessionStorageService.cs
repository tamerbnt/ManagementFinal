using System.Threading.Tasks;

namespace Management.Application.Services
{
    /// <summary>
    /// Service for session-scoped storage (in-memory, per-session data).
    /// </summary>
    public interface ISessionStorageService
    {
        Task<T?> GetAsync<T>(string key) where T : class;
        Task SetAsync<T>(string key, T value) where T : class;
        Task RemoveAsync(string key);
        Task ClearAsync();
    }
}
