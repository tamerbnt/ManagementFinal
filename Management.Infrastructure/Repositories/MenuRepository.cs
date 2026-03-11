using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Management.Domain.Interfaces;
using Management.Domain.Models.Restaurant;
using Management.Infrastructure.Data;

namespace Management.Infrastructure.Repositories
{
    public class MenuRepository : Repository<RestaurantMenuItem>, IMenuRepository
    {
        public MenuRepository(AppDbContext context) : base(context)
        {
        }

        public async Task<IEnumerable<RestaurantMenuItem>> GetByFacilityIdAsync(Guid facilityId)
        {
            return await _dbSet
                .Where(m => m.FacilityId == facilityId && !m.IsDeleted)
                .ToListAsync();
        }
    }
}
