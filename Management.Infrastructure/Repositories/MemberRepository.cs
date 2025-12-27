using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Management.Domain.Enums;
using Management.Domain.Interfaces;
using Management.Domain.Models;
using Management.Infrastructure.Data;

namespace Management.Infrastructure.Repositories
{
    public class MemberRepository : Repository<Member>, IMemberRepository
    {
        public MemberRepository(GymDbContext context) : base(context) { }

        public async Task<IEnumerable<Member>> SearchAsync(string searchTerm, MemberStatus? status = null)
        {
            var query = _dbSet.AsNoTracking();

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                string term = searchTerm.ToLower();
                query = query.Where(m =>
                    m.FullName.ToLower().Contains(term) ||
                    m.Email.ToLower().Contains(term) ||
                    m.CardId.ToLower().Contains(term));
            }

            if (status.HasValue)
            {
                query = query.Where(m => m.Status == status.Value);
            }

            // Default sorting by Name
            return await query.OrderBy(m => m.FullName).ToListAsync();
        }

        public async Task<IEnumerable<Member>> GetExpiringAsync(DateTime threshold)
        {
            return await _dbSet.AsNoTracking()
                .Where(m => m.Status == MemberStatus.Active &&
                            m.ExpirationDate <= threshold &&
                            m.ExpirationDate > DateTime.UtcNow) // Not already expired
                .OrderBy(m => m.ExpirationDate)
                .ToListAsync();
        }

        // --- Dashboard Counters (Optimized: Count logic happens in SQL) ---

        public async Task<int> GetActiveCountAsync()
        {
            return await _dbSet.CountAsync(m => m.Status == MemberStatus.Active);
        }

        public async Task<int> GetTotalCountAsync()
        {
            return await _dbSet.CountAsync();
        }

        public async Task<int> GetExpiringCountAsync(DateTime threshold)
        {
            return await _dbSet.CountAsync(m =>
                m.Status == MemberStatus.Active &&
                m.ExpirationDate <= threshold);
        }
    }
}