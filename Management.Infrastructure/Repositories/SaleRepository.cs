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
        public SaleRepository(GymDbContext context) : base(context) { }

        public async Task<IEnumerable<Sale>> GetByDateRangeAsync(DateTime start, DateTime end)
        {
            return await _dbSet.AsNoTracking()
                .Include(s => s.Items) // Eager load items for receipt details
                .Where(s => s.Timestamp >= start && s.Timestamp <= end)
                .OrderByDescending(s => s.Timestamp)
                .ToListAsync();
        }

        public async Task<decimal> GetTotalRevenueAsync(DateTime start, DateTime end)
        {
            // Performance: Sum happens in database (SQL), not memory
            return await _dbSet
                .Where(s => s.Timestamp >= start && s.Timestamp <= end)
                .SumAsync(s => s.TotalAmount);
        }

        public async Task<IEnumerable<Sale>> GetFailedTransactionsAsync()
        {
            // Assuming failed transactions are logged but flagged via specific logic
            // (e.g., TotalAmount is 0 but items exist, or a status flag if added to model)
            // For now, we return empty or check for specific failure markers if defined
            return await _dbSet.AsNoTracking()
                .Where(s => s.TotalAmount == 0) // Placeholder logic for "Failed"
                .OrderByDescending(s => s.Timestamp)
                .ToListAsync();
        }
    }
}