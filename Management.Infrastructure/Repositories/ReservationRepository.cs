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
    public class ReservationRepository : Repository<Reservation>, IReservationRepository
    {
        public ReservationRepository(AppDbContext context) : base(context) { }

        public async Task<IEnumerable<Reservation>> GetByDateRangeAsync(DateTime start, DateTime end, Guid? facilityId = null)
        {
            var query = facilityId.HasValue
                ? _dbSet.IgnoreQueryFilters().Where(p => p.FacilityId == facilityId.Value && p.StartTime >= start && p.StartTime <= end)
                : _dbSet.AsNoTracking().Where(r => r.StartTime >= start && r.StartTime <= end);

            return await query
                .OrderBy(r => r.StartTime)
                .ToListAsync();
        }

        public async Task<IEnumerable<Reservation>> GetByMemberIdAsync(Guid memberId, Guid? facilityId = null)
        {
            var query = facilityId.HasValue
                ? _dbSet.IgnoreQueryFilters().Where(p => p.FacilityId == facilityId.Value && p.MemberId == memberId)
                : _dbSet.AsNoTracking().Where(r => r.MemberId == memberId);

            return await query
                .OrderByDescending(r => r.StartTime)
                .ToListAsync();
        }

        public override async Task<Reservation?> GetByIdAsync(Guid id, Guid? facilityId = null)
        {
            if (facilityId.HasValue)
            {
                return await _dbSet.IgnoreQueryFilters()
                    .FirstOrDefaultAsync(p => p.Id == id && p.FacilityId == facilityId.Value);
            }
            return await base.GetByIdAsync(id);
        }
    }
}
