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
    public class TransactionRepository : Repository<Transaction>, ITransactionRepository
    {
        public TransactionRepository(AppDbContext context) : base(context) { }

        public async Task<IEnumerable<Transaction>> GetRecentHistoryAsync(Guid facilityId, int count)
        {
            return await _dbSet.IgnoreQueryFilters()
                .Where(t => t.FacilityId == facilityId && !t.IsDeleted)
                .OrderByDescending(t => t.Timestamp)
                .Take(count)
                .ToListAsync();
        }

        public async Task<IEnumerable<Transaction>> GetByRangeAsync(Guid facilityId, DateTime start, DateTime end)
        {
            return await _dbSet.IgnoreQueryFilters()
                .Where(t => t.FacilityId == facilityId && !t.IsDeleted && t.Timestamp >= start && t.Timestamp <= end)
                .OrderByDescending(t => t.Timestamp)
                .ToListAsync();
        }

        public override async Task<Transaction?> GetByIdAsync(Guid id, Guid? facilityId = null)
        {
            if (facilityId.HasValue)
            {
                return await _dbSet.IgnoreQueryFilters()
                    .FirstOrDefaultAsync(p => p.Id == id && p.FacilityId == facilityId.Value && !p.IsDeleted);
            }
            return await base.GetByIdAsync(id);
        }

        public async Task UpdateAuditNoteAsync(Guid transactionId, string note)
        {
            var transaction = await GetByIdAsync(transactionId);
            if (transaction != null)
            {
                transaction.AuditNote = note;
                // Explicitly save since we modified a tracked entity and this method is atomic
                await _context.SaveChangesAsync();
            }
        }
    }
}
