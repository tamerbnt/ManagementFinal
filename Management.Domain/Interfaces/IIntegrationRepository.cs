using System.Threading.Tasks;
using Management.Domain.Models;

namespace Management.Domain.Interfaces
{
    public interface IIntegrationRepository : IRepository<IntegrationConfig>
    {
        Task<IntegrationConfig?> GetByProviderAsync(string providerName, System.Guid? facilityId = null);
        Task<IntegrationConfig?> GetByIdAsync(System.Guid id, System.Guid? facilityId = null);
    }
}
