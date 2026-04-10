using System;
using System.Threading.Tasks;
using Management.Domain.Interfaces;
using Management.Domain.Models;
using Management.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Management.Infrastructure.Repositories
{
    public class SalonSettingsRepository : ISalonSettingsRepository
    {
        private readonly AppDbContext _context;
        private readonly DbSet<SalonSettings> _dbSet;

        public SalonSettingsRepository(AppDbContext context)
        {
            _context = context;
            _dbSet = context.Set<SalonSettings>();
        }

        public async Task<SalonSettings> GetAsync(Guid facilityId)
        {
            // Attempt to fetch the singleton row for this facility
            var settings = await _dbSet.IgnoreQueryFilters()
                .FirstOrDefaultAsync(s => s.FacilityId == facilityId && !s.IsDeleted);

            if (settings == null)
            {
                // Safety Guard: Don't auto-seed for empty facility ID
                if (facilityId == Guid.Empty)
                    return new SalonSettings { FacilityId = facilityId };

                // Seed default settings if none exist
                settings = new SalonSettings
                {
                    Id = Guid.NewGuid(),
                    FacilityId = facilityId,
                    TotalChairs = 1,
                    DailyRevenueTarget = 1000m,
                    OperatingHoursJson = "{}"
                };

                _dbSet.Add(settings);
                await _context.SaveChangesAsync();
            }

            return settings;
        }

        public async Task SaveAsync(SalonSettings settings)
        {
            var existing = _dbSet.Local.FirstOrDefault(s => s.Id == settings.Id) 
                ?? await _dbSet.FirstOrDefaultAsync(s => s.Id == settings.Id);

            if (existing == null)
            {
                _dbSet.Add(settings);
            }
            else if (existing != settings)
            {
                _context.Entry(existing).CurrentValues.SetValues(settings);
            }

            await _context.SaveChangesAsync();
        }
    }
}
