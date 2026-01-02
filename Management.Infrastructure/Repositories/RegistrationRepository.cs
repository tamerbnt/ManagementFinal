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
    public class RegistrationRepository : Repository<Registration>, IRegistrationRepository
    {
        public RegistrationRepository(GymDbContext context) : base(context) { }

        public async Task<IEnumerable<Registration>> GetPendingRegistrationsAsync()
        {
            return await _dbSet.AsNoTracking()
                .Where(r => r.Status == RegistrationStatus.Pending)
                .OrderByDescending(r => r.CreatedAt) // Newest first
                .ToListAsync();
        }

        public async Task<int> GetCountByStatusAsync(RegistrationStatus status)
        {
            // Optimized SQL Count query
            return await _dbSet.CountAsync(r => r.Status == status);
        }
    }
}