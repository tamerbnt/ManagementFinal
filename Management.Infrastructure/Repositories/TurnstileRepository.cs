using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Management.Domain.Interfaces;
using Management.Domain.Models;
using Management.Infrastructure.Data;

namespace Management.Infrastructure.Repositories
{
    public class TurnstileRepository : Repository<Turnstile>, ITurnstileRepository
    {
        public TurnstileRepository(GymDbContext context) : base(context) { }

        public async Task<Turnstile> GetByHardwareIdAsync(string hardwareId)
        {
            // Returns null if not found, allowing service to handle registration logic
            return await _dbSet.FirstOrDefaultAsync(t => t.HardwareId == hardwareId);
        }
    }
}