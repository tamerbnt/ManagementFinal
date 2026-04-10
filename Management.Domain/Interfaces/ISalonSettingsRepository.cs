using System;
using System.Threading.Tasks;
using Management.Domain.Models;

namespace Management.Domain.Interfaces
{
    public interface ISalonSettingsRepository
    {
        Task<SalonSettings> GetAsync(Guid facilityId);
        Task SaveAsync(SalonSettings settings);
    }
}
