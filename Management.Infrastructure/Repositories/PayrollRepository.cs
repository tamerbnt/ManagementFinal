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
    public class PayrollRepository : Repository<PayrollEntry>, IPayrollRepository
    {
        public PayrollRepository(AppDbContext context) : base(context) { }

        public async Task<List<PayrollEntry>> GetByStaffIdAsync(Guid staffId, Guid? facilityId = null)
        {
            var query = facilityId.HasValue
                ? _dbSet.IgnoreQueryFilters().Where(p => p.StaffId == staffId && p.FacilityId == facilityId.Value && !p.IsDeleted)
                : _dbSet.AsNoTracking().Where(p => p.StaffId == staffId && !p.IsDeleted);

            return await query
                .OrderByDescending(p => p.PayPeriodEnd)
                .ToListAsync();
        }

        public override async Task<PayrollEntry?> GetByIdAsync(Guid id, Guid? facilityId = null)
        {
            if (facilityId.HasValue)
            {
                return await _dbSet.IgnoreQueryFilters()
                    .FirstOrDefaultAsync(p => p.Id == id && p.FacilityId == facilityId.Value && !p.IsDeleted);
            }
            return await base.GetByIdAsync(id);
        }

        public async Task<List<PayrollEntry>> GetByRangeAsync(Guid facilityId, DateTime start, DateTime end)
        {
            return await _dbSet.IgnoreQueryFilters()
                .Where(p => p.FacilityId == facilityId && 
                            (p.UpdatedAt ?? p.CreatedAt) >= start && 
                            (p.UpdatedAt ?? p.CreatedAt) <= end && 
                            !p.IsDeleted)
                .OrderByDescending(p => p.UpdatedAt ?? p.CreatedAt)
                .ToListAsync();
        }

        public async Task RestoreAsync(Guid id)
        {
            var entry = await _dbSet.IgnoreQueryFilters().FirstOrDefaultAsync(p => p.Id == id);
            if (entry != null)
            {
                entry.Restore();
                await _context.SaveChangesAsync();
            }
        }
    }
}
