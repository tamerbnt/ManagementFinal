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
        Task<GymSettings> GetAsync(System.Guid facilityId);
        Task SaveAsync(GymSettings settings);
    }
}
