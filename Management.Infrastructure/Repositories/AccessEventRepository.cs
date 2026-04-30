using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Management.Domain.Enums;
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

        public async Task<IEnumerable<AccessEvent>> GetByDateRangeAsync(Guid facilityId, DateTime start, DateTime end, bool includeDeleted = false)
        {
            return await _dbSet.IgnoreQueryFilters()
                .Where(e => e.FacilityId == facilityId && (includeDeleted || !e.IsDeleted) && e.Timestamp >= start && e.Timestamp <= end)
                .OrderByDescending(e => e.Timestamp)
                .ToListAsync();
        }

        public async Task<int> GetCurrentOccupancyCountAsync(Guid facilityId)
        {
            var todayUtc = DateTime.UtcNow.Date;
            var tomorrowUtc = todayUtc.AddDays(1);

            var entries = await _dbSet.IgnoreQueryFilters()
                .Where(e => e.FacilityId == facilityId
                    && !e.IsDeleted
                    && e.IsAccessGranted
                    && e.Direction == ScanDirection.Enter
                    && e.Timestamp >= todayUtc
                    && e.Timestamp < tomorrowUtc)
                .CountAsync();

            var exits = await _dbSet.IgnoreQueryFilters()
                .Where(e => e.FacilityId == facilityId
                    && !e.IsDeleted
                    && e.Direction == ScanDirection.Exit
                    && e.Timestamp >= todayUtc
                    && e.Timestamp < tomorrowUtc)
                .CountAsync();

            // Never return negative — handles case where exit scans
            // exist without matching entry (e.g. after app restart)
            return Math.Max(0, entries - exits);
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
        public async Task<AccessEvent?> GetByTransactionIdAsync(string transactionId)
        {
            return await _dbSet.IgnoreQueryFilters()
                .FirstOrDefaultAsync(e => e.TransactionId == transactionId && !e.IsDeleted);
        }

        public async Task<IEnumerable<Member>> GetActiveMembersAsync(Guid facilityId)
        {
            var todayUtc = DateTime.UtcNow.Date;
            var tomorrowUtc = todayUtc.AddDays(1);

            // 1. Get all successful entries for today
            var entries = await _dbSet.IgnoreQueryFilters()
                .Where(e => e.FacilityId == facilityId
                    && !e.IsDeleted
                    && e.IsAccessGranted
                    && e.Direction == ScanDirection.Enter
                    && e.Timestamp >= todayUtc
                    && e.Timestamp < tomorrowUtc)
                .Select(e => new { e.CardId, e.Timestamp })
                .ToListAsync();

            if (!entries.Any()) return Enumerable.Empty<Member>();

            // 2. Get all exits for today
            var exits = await _dbSet.IgnoreQueryFilters()
                .Where(e => e.FacilityId == facilityId
                    && !e.IsDeleted
                    && e.Direction == ScanDirection.Exit
                    && e.Timestamp >= todayUtc
                    && e.Timestamp < tomorrowUtc)
                .Select(e => new { e.CardId, e.Timestamp })
                .ToListAsync();

            // 3. Find CardIds that are currently "inside" 
            // (most recent event is an entry, or entry count > exit count)
            var activeCardIds = entries
                .GroupBy(e => e.CardId)
                .Where(g => 
                {
                    var lastEntry = g.Max(e => e.Timestamp);
                    var lastExit = exits.Where(x => x.CardId == g.Key).Select(x => x.Timestamp).DefaultIfEmpty(DateTime.MinValue).Max();
                    return lastEntry > lastExit;
                })
                .Select(g => g.Key)
                .ToList();

            if (!activeCardIds.Any()) return Enumerable.Empty<Member>();

            // 4. Resolve Members
            return await _context.Set<Member>()
                .Where(m => activeCardIds.Contains(m.CardId))
                .ToListAsync();
        }
    }
}
