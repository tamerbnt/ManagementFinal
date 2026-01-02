using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Management.Domain.Interfaces;
using Management.Domain.Models;
using Management.Infrastructure.Data;

namespace Management.Infrastructure.Repositories
{
    public class IntegrationRepository : Repository<IntegrationConfig>, IIntegrationRepository
    {
        public IntegrationRepository(GymDbContext context) : base(context) { }

        public async Task<IntegrationConfig?> GetByProviderAsync(string providerName)
        {
            return await _dbSet.FirstOrDefaultAsync(i => i.ProviderName == providerName);
        }
    }
}