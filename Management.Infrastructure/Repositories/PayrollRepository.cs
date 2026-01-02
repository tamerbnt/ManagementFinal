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
        public PayrollRepository(GymDbContext context) : base(context) { }

        public async Task<IEnumerable<PayrollEntry>> GetByStaffIdAsync(Guid staffId)
        {
            return await _dbSet.AsNoTracking()
                .Where(p => p.StaffMemberId == staffId)
                .OrderByDescending(p => p.PayPeriodEnd)
                .ToListAsync();
        }
    }
}