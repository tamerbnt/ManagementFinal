using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Management.Domain.Interfaces;
using Management.Domain.Models;
using Management.Infrastructure.Data;

namespace Management.Infrastructure.Repositories
{
    public class TurnstileRepository : Repository<Turnstile>, ITurnstileRepository
    {
        public TurnstileRepository(AppDbContext context) : base(context) { }

        public async Task<Turnstile?> GetByHardwareIdAsync(string hardwareId, System.Guid? facilityId = null)
        {
            if (facilityId.HasValue)
            {
                return await _dbSet.IgnoreQueryFilters()
                    .FirstOrDefaultAsync(t => t.HardwareId == hardwareId && t.FacilityId == facilityId.Value && !t.IsDeleted);
            }
            return await _dbSet.FirstOrDefaultAsync(t => t.HardwareId == hardwareId);
        }

        public override async Task<Turnstile?> GetByIdAsync(System.Guid id, System.Guid? facilityId = null)
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
