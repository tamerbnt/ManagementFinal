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
        public AccessEventRepository(GymDbContext context) : base(context) { }

        public async Task<IEnumerable<AccessEvent>> GetRecentEventsAsync(int count)
        {
            // Critical for Live Feed: Fetch newest first, limited count
            return await _dbSet.AsNoTracking()
                .OrderByDescending(e => e.Timestamp)
                .Take(count)
                .ToListAsync();
        }

        public async Task<IEnumerable<AccessEvent>> GetByDateRangeAsync(DateTime start, DateTime end)
        {
            return await _dbSet.AsNoTracking()
                .Where(e => e.Timestamp >= start && e.Timestamp <= end)
                .OrderByDescending(e => e.Timestamp)
                .ToListAsync();
        }

        public async Task<int> GetCurrentOccupancyCountAsync()
        {
            // Logic: Count valid entries since the start of the day.
            // In a full implementation with Entry/Exit turnstiles, this would be (Entries - Exits).
            // For V1, we calculate daily unique footfall or raw granted access counts.
            var today = DateTime.UtcNow.Date;

            return await _dbSet.CountAsync(e =>
                e.Timestamp >= today &&
                e.IsAccessGranted);
        }
    }
}