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
    public class TableRepository : Repository<TableModel>, ITableRepository
    {
        public TableRepository(AppDbContext context) : base(context)
        {
        }

        public async Task<IEnumerable<TableModel>> GetByFacilityIdAsync(Guid facilityId)
        {
            return await _dbSet
                .AsNoTracking()
                .Where(t => t.FacilityId == facilityId && !t.IsDeleted)
                .ToListAsync();
        }
    }
}
