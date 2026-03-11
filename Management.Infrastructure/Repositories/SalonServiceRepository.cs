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
    }
}
