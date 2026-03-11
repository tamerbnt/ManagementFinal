using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Management.Domain.Interfaces;
using Management.Domain.Models;
using Management.Infrastructure.Data;

namespace Management.Infrastructure.Repositories
{
    public class AccessEventRepository : Repository<AccessEvent>, IAccessEventRepository
    {
        public AccessEventRepository(AppDbContext context) : base(context) { }

        public async Task<IEnumerable<AccessEvent>> GetRecentEventsAsync(Guid facilityId, int count)
        {
            return await _dbSet.IgnoreQueryFilters()
                .Where(e => e.FacilityId == facilityId && !e.IsDeleted)
                .OrderByDescending(e => e.Timestamp)
                .Take(count)
                .ToListAsync();
        }

        public async Task<IEnumerable<AccessEvent>> GetByDateRangeAsync(Guid facilityId, DateTime start, DateTime end)
        {
            return await _dbSet.IgnoreQueryFilters()
                .Where(e => e.FacilityId == facilityId && !e.IsDeleted && e.Timestamp >= start && e.Timestamp <= end)
                .OrderByDescending(e => e.Timestamp)
                .ToListAsync();
        }

        public async Task<int> GetCurrentOccupancyCountAsync(Guid facilityId)
        {
            // Calculate start of today in LOCAL time, then convert to UTC for DB query
            var localTodayStart = DateTime.Today; // 00:00:00 Local
            var todayUtcStart = localTodayStart.ToUniversalTime();

            return await _dbSet.IgnoreQueryFilters().CountAsync(e =>
                e.FacilityId == facilityId &&
                !e.IsDeleted &&
                e.Timestamp >= todayUtcStart &&
                e.IsAccessGranted);
        }

        public async Task<int> GetVisitCountAsync(Guid memberId)
        {
            var cardId = await _context.Set<Member>()
                .Where(m => m.Id == memberId)
                .Select(m => m.CardId)
                .FirstOrDefaultAsync();

            if (string.IsNullOrEmpty(cardId)) return 0;

            return await _dbSet.IgnoreQueryFilters().CountAsync(e =>
                e.CardId == cardId &&
                !e.IsDeleted &&
                e.IsAccessGranted);
        }

        public async Task<IEnumerable<AccessEvent>> GetByMemberIdAsync(Guid memberId)
        {
            var cardId = await _context.Set<Member>()
                .Where(m => m.Id == memberId)
                .Select(m => m.CardId)
                .FirstOrDefaultAsync();

            if (string.IsNullOrEmpty(cardId)) return Enumerable.Empty<AccessEvent>();

            return await _dbSet.IgnoreQueryFilters()
                .Where(e => e.CardId == cardId && !e.IsDeleted)
                .OrderByDescending(e => e.Timestamp)
                .ToListAsync();
        }

        public override async Task<AccessEvent?> GetByIdAsync(Guid id, Guid? facilityId = null)
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
