using Management.Domain.Interfaces;
using Management.Domain.Models;
using Management.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Management.Infrastructure.Repositories
{
    public class GymSettingsRepository : IGymSettingsRepository
    {
        private readonly AppDbContext _context;
        private readonly DbSet<GymSettings> _dbSet;

        public GymSettingsRepository(AppDbContext context)
        {
            _context = context;
            _dbSet = context.Set<GymSettings>();
        }

        public async Task<GymSettings> GetAsync(Guid facilityId)
        {
            // Attempt to fetch the singleton row for this facility
            var settings = await _dbSet.IgnoreQueryFilters()
                .FirstOrDefaultAsync(s => s.FacilityId == facilityId && !s.IsDeleted);

            if (settings == null)
            {
                // Safety Guard: Don't auto-seed for empty facility ID (prevents Data Isolation Violation)
                if (facilityId == Guid.Empty)
                {
                    return new GymSettings { FacilityId = Guid.Empty };
                }

                // Auto-Seed: Create default settings if not found for this facility
                settings = new GymSettings
                {
                    FacilityId = facilityId,
                    GymName = "My Gym",
                    MaxOccupancy = 100,
                    IsMaintenanceMode = false,
                    // Default empty JSON structure if needed
                    OperatingHoursJson = "{}"
                };

                await _dbSet.AddAsync(settings);
                await _context.SaveChangesAsync();
            }

            return settings;
        }

        public async Task SaveAsync(GymSettings settings)
        {
            if (_context.Entry(settings).State == EntityState.Detached)
            {
                _dbSet.Attach(settings);
            }

            _context.Entry(settings).State = EntityState.Modified;

            // Allow Auditing to pick up UpdatedAt
            await _context.SaveChangesAsync();
        }
    }
}
