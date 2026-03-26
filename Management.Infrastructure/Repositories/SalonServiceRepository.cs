using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Management.Domain.Interfaces;
using Management.Domain.Models.Salon;
using Management.Infrastructure.Data;

namespace Management.Infrastructure.Repositories
{
    public class SalonServiceRepository : Repository<SalonService>, ISalonServiceRepository
    {
        public SalonServiceRepository(AppDbContext context) : base(context) { }

        public async Task<IEnumerable<SalonService>> GetAllAsync(Guid facilityId)
        {
            return await _dbSet.AsNoTracking()
                .Where(s => s.FacilityId == facilityId)
                .ToListAsync();
        }

        public override async Task<SalonService?> GetByIdAsync(Guid id, Guid? facilityId = null)
        {
            var query = _dbSet.AsNoTracking();
            if (facilityId.HasValue)
            {
                query = query.IgnoreQueryFilters().Where(s => s.FacilityId == facilityId.Value);
            }
            return await query.FirstOrDefaultAsync(s => s.Id == id);
        }

        public override async Task RestoreAsync(Guid id, Guid? facilityId = null)
        {
            var query = _dbSet.IgnoreQueryFilters();
            if (facilityId.HasValue)
                query = query.Where(p => p.FacilityId == facilityId.Value);

            var service = await query.FirstOrDefaultAsync(p => p.Id == id);
            if (service != null)
            {
                service.Restore();
                // SalonService doesn't have IsActive, but we follow the pattern for consistency
                // if it's ever added or if we want to ensure any other flags are reset.
                await _context.SaveChangesAsync();
            }
        }
    }
}
