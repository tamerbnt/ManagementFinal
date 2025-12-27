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
        private readonly GymDbContext _context;
        private readonly DbSet<GymSettings> _dbSet;

        public GymSettingsRepository(GymDbContext context)
        {
            _context = context;
            _dbSet = context.Set<GymSettings>();
        }

        public async Task<GymSettings> GetAsync()
        {
            // Attempt to fetch the singleton row
            var settings = await _dbSet.FirstOrDefaultAsync();

            if (settings == null)
            {
                // Auto-Seed: Create default settings if database is empty
                settings = new GymSettings
                {
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