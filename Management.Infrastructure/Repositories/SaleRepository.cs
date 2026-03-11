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
    public class SaleRepository : Repository<Sale>, ISaleRepository
    {
        public SaleRepository(AppDbContext context) : base(context) { }

        public async Task<IEnumerable<Sale>> GetByDateRangeAsync(Guid facilityId, DateTime start, DateTime end)
        {
            return await _dbSet.AsNoTracking()
                .IgnoreQueryFilters() // Bypass global filter to allow explicit facility override
                .Include(s => s.Items) // Eager load items for receipt details
                .Where(s => s.FacilityId == facilityId && s.Timestamp >= start && s.Timestamp <= end)
                .OrderByDescending(s => s.Timestamp)
                .ToListAsync();
        }

        public async Task<decimal> GetTotalRevenueAsync(Guid facilityId, DateTime start, DateTime end)
        {
            // Performance: Sum happens in database (SQL), not memory
            // Fix: SQLite cannot apply aggregate operator 'Sum' on expressions of type 'decimal'.
            // Casting to double for the aggregate operation.
            return (decimal)await _dbSet
                .IgnoreQueryFilters() // Bypass global filter to allow explicit facility override
                .Where(s => s.FacilityId == facilityId && s.Timestamp >= start && s.Timestamp <= end)
                .SumAsync(s => (double)s.TotalAmount.Amount);
        }

        public async Task<IEnumerable<Sale>> GetFailedTransactionsAsync(Guid facilityId)
        {
            return await _dbSet.AsNoTracking()
                .IgnoreQueryFilters()
                .Where(s => s.FacilityId == facilityId && s.TotalAmount.Amount == 0 && !s.IsDeleted) 
                .OrderByDescending(s => s.Timestamp)
                .ToListAsync();
        }

        public override async Task<Sale?> GetByIdAsync(Guid id, Guid? facilityId = null)
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
