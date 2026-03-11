using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Management.Domain.Models;
using Management.Domain.Interfaces;
using Management.Infrastructure.Data;

namespace Management.Infrastructure.Repositories
{
    public class FacilityScheduleRepository : Repository<FacilitySchedule>, IFacilityScheduleRepository
    {
        public FacilityScheduleRepository(AppDbContext context) : base(context) { }

        public async Task<IEnumerable<FacilitySchedule>> GetByFacilityIdAsync(Guid facilityId)
        {
            return await _dbSet.IgnoreQueryFilters()
                .Where(s => s.FacilityId == facilityId && !s.IsDeleted)
                .OrderBy(s => s.DayOfWeek)
                .ThenBy(s => s.StartTime)
                .ToListAsync();
        }

        public override async Task<FacilitySchedule?> GetByIdAsync(Guid id, Guid? facilityId = null)
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
