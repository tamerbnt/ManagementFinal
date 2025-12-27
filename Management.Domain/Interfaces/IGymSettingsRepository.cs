using System.Threading.Tasks;
using Management.Domain.Models;

namespace Management.Domain.Interfaces
{
    /// <summary>
    /// Special repository for the Singleton configuration row.
    /// </summary>
    public interface IGymSettingsRepository
    {
        // Returns the single active configuration row. Creates default if missing.
        Task<GymSettings> GetAsync();

        // Updates the singleton.
        Task SaveAsync(GymSettings settings);
    }
}