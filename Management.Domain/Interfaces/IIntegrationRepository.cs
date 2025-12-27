using System.Threading.Tasks;
using Management.Domain.Models;

namespace Management.Domain.Interfaces
{
    public interface IIntegrationRepository : IRepository<IntegrationConfig>
    {
        Task<IntegrationConfig> GetByProviderAsync(string providerName);
    }
}