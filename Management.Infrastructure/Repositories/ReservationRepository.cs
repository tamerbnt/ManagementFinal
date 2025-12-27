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
        public ReservationRepository(GymDbContext context) : base(context) { }

        public async Task<IEnumerable<Reservation>> GetByDateRangeAsync(DateTime start, DateTime end)
        {
            return await _dbSet.AsNoTracking()
                .Where(r => r.StartTime >= start && r.StartTime <= end)
                .OrderBy(r => r.StartTime)
                .ToListAsync();
        }

        public async Task<IEnumerable<Reservation>> GetByMemberIdAsync(Guid memberId)
        {
            return await _dbSet.AsNoTracking()
                .Where(r => r.MemberId == memberId)
                .OrderByDescending(r => r.StartTime)
                .ToListAsync();
        }
    }
}