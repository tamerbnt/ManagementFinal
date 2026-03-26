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
    public class AppointmentRepository : Repository<Appointment>, IAppointmentRepository
    {
        public AppointmentRepository(AppDbContext context) : base(context) { }

        public async Task<IEnumerable<Appointment>> GetByDateRangeAsync(DateTime start, DateTime end, Guid? facilityId = null)
        {
            var query = facilityId.HasValue
                ? _dbSet.Include(a => a.UsedProducts).IgnoreQueryFilters().AsNoTracking().Where(a => a.FacilityId == facilityId.Value && !a.IsDeleted && a.StartTime >= start && a.StartTime <= end)
                : _dbSet.Include(a => a.UsedProducts).AsNoTracking().Where(a => !a.IsDeleted && a.StartTime >= start && a.StartTime <= end);

            return await query
                .OrderBy(a => a.StartTime)
                .ToListAsync();
        }

        public async Task<IEnumerable<Appointment>> GetByStaffAsync(Guid staffId, DateTime start, DateTime end, Guid? facilityId = null)
        {
            var query = facilityId.HasValue
                ? _dbSet.Include(a => a.UsedProducts).IgnoreQueryFilters().Where(a => a.FacilityId == facilityId.Value && !a.IsDeleted && a.StaffId == staffId && a.StartTime >= start && a.StartTime <= end)
                : _dbSet.Include(a => a.UsedProducts).AsNoTracking().Where(a => !a.IsDeleted && a.StaffId == staffId && a.StartTime >= start && a.StartTime <= end);

            return await query
                .OrderBy(a => a.StartTime)
                .ToListAsync();
        }

        public async Task<bool> HasConflictAsync(Guid staffId, DateTime start, DateTime end, Guid? excludingAppointmentId = null, Guid? facilityId = null)
        {
            var query = facilityId.HasValue
                ? _dbSet.IgnoreQueryFilters().Where(a => a.FacilityId == facilityId.Value && !a.IsDeleted && a.StaffId == staffId && a.StartTime < end && a.EndTime > start)
                : _dbSet.AsNoTracking().Where(a => !a.IsDeleted && a.StaffId == staffId && a.StartTime < end && a.EndTime > start);

            if (excludingAppointmentId.HasValue)
            {
                query = query.Where(a => a.Id != excludingAppointmentId.Value);
            }

            return await query.AnyAsync();
        }

        public override async Task<Appointment?> GetByIdAsync(Guid id, Guid? facilityId = null)
        {
            if (facilityId.HasValue)
            {
                return await _dbSet.Include(a => a.UsedProducts).IgnoreQueryFilters()
                    .FirstOrDefaultAsync(p => p.Id == id && p.FacilityId == facilityId.Value && !p.IsDeleted);
            }

            return await _dbSet
                .Include(a => a.UsedProducts)
                .FirstOrDefaultAsync(a => a.Id == id && !a.IsDeleted);
        }

        public override async Task RestoreAsync(Guid id, Guid? facilityId = null)
        {
            await _dbSet
                .IgnoreQueryFilters()
                .Where(a => a.Id == id)
                .ExecuteUpdateAsync(a => a.SetProperty(x => x.IsDeleted, false));
        }
    }
}
