using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Management.Domain.Interfaces;
using Management.Domain.Models;
using Management.Infrastructure.Data;

namespace Management.Infrastructure.Repositories
{
    public class IntegrationRepository : Repository<IntegrationConfig>, IIntegrationRepository
    {
        public IntegrationRepository(AppDbContext context) : base(context) { }

        public async Task<IntegrationConfig?> GetByProviderAsync(string providerName, System.Guid? facilityId = null)
        {
            if (facilityId.HasValue)
            {
                return await _dbSet.IgnoreQueryFilters()
                    .FirstOrDefaultAsync(i => i.ProviderName == providerName && i.FacilityId == facilityId.Value && !i.IsDeleted);
            }
            return await _dbSet.FirstOrDefaultAsync(i => i.ProviderName == providerName);
        }

        public override async Task<IntegrationConfig?> GetByIdAsync(System.Guid id, System.Guid? facilityId = null)
        {
            if (facilityId.HasValue)
            {
                return await _dbSet.IgnoreQueryFilters()
                    .FirstOrDefaultAsync(p => p.Id == id && p.FacilityId == facilityId.Value && !p.IsDeleted);
            }
            return await base.GetByIdAsync(id);
        }
    }
}
