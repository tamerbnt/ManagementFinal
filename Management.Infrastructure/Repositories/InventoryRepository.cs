using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Management.Domain.Interfaces;
using Management.Domain.Models;
using Management.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Management.Infrastructure.Repositories
{
    public class InventoryRepository : IInventoryRepository
    {
        private readonly AppDbContext _dbContext;

        public InventoryRepository(AppDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task AddTransactionAsync(InventoryTransaction transaction)
        {
            await _dbContext.InventoryTransactions.AddAsync(transaction);
        }

        public async Task<List<InventoryTransaction>> GetHistoryAsync(Guid facilityId, int? limit = null)
        {
            var query = _dbContext.InventoryTransactions
                .Where(t => t.FacilityId == facilityId)
                .OrderByDescending(t => t.Timestamp)
                .AsQueryable();

            if (limit.HasValue)
            {
                query = query.Take(limit.Value);
            }

            return await query.ToListAsync();
        }

        public async Task<List<InventoryTransaction>> GetHistoryByProductAsync(Guid productId, Guid facilityId)
        {
            return await _dbContext.InventoryTransactions
                .Where(t => t.ProductId == productId && t.FacilityId == facilityId)
                .OrderByDescending(t => t.Timestamp)
                .ToListAsync();
        }
    }
}
