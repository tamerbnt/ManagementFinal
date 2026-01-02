using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Management.Domain.Interfaces;
using Management.Domain.Models;
using Management.Infrastructure.Data;

namespace Management.Infrastructure.Repositories
{
    public class StaffRepository : Repository<StaffMember>, IStaffRepository
    {
        public StaffRepository(GymDbContext context) : base(context) { }

        public async Task<StaffMember?> GetByEmailAsync(string email)
        {
            // Note: Not throwing exception here; return null to allow Service to handle "Invalid Login"
            return await _dbSet.FirstOrDefaultAsync(s => s.Email.Value == email);
        }

        public async Task<IEnumerable<StaffMember>> GetAllActiveAsync()
        {
            return await _dbSet.AsNoTracking()
                .Where(s => s.IsActive)
                .OrderBy(s => s.FullName)
                .ToListAsync();
        }
    }
}