using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Management.Domain.Interfaces;
using Management.Domain.Models;
using Management.Infrastructure.Data;

namespace Management.Infrastructure.Repositories
{
    public class MembershipPlanRepository : Repository<MembershipPlan>, IMembershipPlanRepository
    {
        public MembershipPlanRepository(AppDbContext context) : base(context) { }

        public override async Task<MembershipPlan?> GetByIdAsync(System.Guid id, System.Guid? facilityId = null)
        {
            var query = _dbSet.AsNoTracking();

            if (facilityId.HasValue)
            {
                query = query.IgnoreQueryFilters().Where(p => p.FacilityId == facilityId.Value);
            }

            return await query
                .Include(p => p.AccessibleFacilities)
                .FirstOrDefaultAsync(p => p.Id == id);
        }

        public async Task<IEnumerable<MembershipPlan>> GetActivePlansAsync(System.Guid? facilityId = null, bool activeOnly = true)
        {
            var query = _dbSet.AsNoTracking();

            if (facilityId.HasValue)
            {
                query = query.IgnoreQueryFilters().Where(p => p.FacilityId == facilityId.Value && !p.IsDeleted);
            }
            else
            {
                query = query.Where(p => !p.IsDeleted);
            }

            if (activeOnly)
            {
                query = query.Where(p => p.IsActive);
            }

            return await query
                .Include(p => p.AccessibleFacilities)
                .ToListAsync();
        }
    }
}
