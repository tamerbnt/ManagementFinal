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
        public MembershipPlanRepository(GymDbContext context) : base(context) { }

        public async Task<IEnumerable<MembershipPlan>> GetActivePlansAsync()
        {
            return await _dbSet.AsNoTracking()
                .Where(p => p.IsActive)
                .OrderBy(p => p.Price.Amount)
                .ToListAsync();
        }
    }
}