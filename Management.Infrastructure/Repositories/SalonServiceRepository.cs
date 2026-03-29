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
            // Direct SQL UPDATE to bypass change tracker conflicts
            var query = _dbSet.IgnoreQueryFilters().Where(p => p.Id == id);
            if (facilityId.HasValue)
                query = query.Where(p => p.FacilityId == facilityId.Value);

            await query.ExecuteUpdateAsync(s => s
                .SetProperty(p => p.IsDeleted, false)
                .SetProperty(p => p.UpdatedAt, DateTime.UtcNow));

            // Sync Tracker: Prevent stale 'Deleted' state from overwriting DB on next SaveChanges
            var tracked = _context.ChangeTracker.Entries<SalonService>()
                .FirstOrDefault(e => e.Entity.Id == id);

            if (tracked != null)
            {
                tracked.Entity.Restore();
                tracked.State = EntityState.Unchanged;
                Serilog.Log.Information("[SalonServiceRepository] Balanced tracker for restored service: {Id}", id);
            }
        }
    }
}
