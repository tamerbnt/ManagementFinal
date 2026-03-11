using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Management.Application.Interfaces;
using Management.Infrastructure.Data;
using Management.Domain.Models;

namespace Management.Infrastructure.Services
{
    public class MembershipService : IMembershipService
    {
        private readonly AppDbContext _context;

        public MembershipService(AppDbContext context)
        {
            _context = context;
        }

        public async Task<List<MembershipPlan>> GetAllPlansAsync()
        {
            return await _context.MembershipPlans
                .Include(p => p.AccessibleFacilities)
                .ToListAsync();
        }

        public async Task<List<Facility>> GetAllFacilitiesAsync()
        {
            return await _context.Facilities.ToListAsync();
        }

        public async Task SavePlanAsync(MembershipPlan plan, List<Guid> selectedFacilityIds)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var existingPlan = await _context.MembershipPlans
                    .Include(p => p.AccessibleFacilities)
                    .FirstOrDefaultAsync(p => p.Id == plan.Id);

                if (existingPlan == null)
                {
                    // New Plan: Ensure ID is set (UUID v7 style if possible elsewhere, but Guid.NewGuid() is fine here)
                    if (plan.Id == Guid.Empty) plan.Id = Guid.NewGuid();
                    await _context.MembershipPlans.AddAsync(plan);
                    existingPlan = plan;
                }
                else
                {
                    // Update basic properties
                    _context.Entry(existingPlan).CurrentValues.SetValues(plan);
                    existingPlan.UpdateTimestamp();
                }

                // Sync Many-to-Many - Junction table update
                var currentFacilities = existingPlan.AccessibleFacilities.ToList();
                var currentFacilityIds = currentFacilities.Select(f => f.Id).ToList();
                
                // Remove facilities no longer selected
                var toRemove = currentFacilities
                    .Where(f => !selectedFacilityIds.Contains(f.Id))
                    .ToList();
                foreach (var facility in toRemove)
                {
                    existingPlan.AccessibleFacilities.Remove(facility);
                }

                // Add newly selected facilities
                var toAddIds = selectedFacilityIds.Except(currentFacilityIds).ToList();
                if (toAddIds.Any())
                {
                    var facilitiesToAdd = await _context.Facilities
                        .Where(f => toAddIds.Contains(f.Id))
                        .ToListAsync();

                    foreach (var facility in facilitiesToAdd)
                    {
                        existingPlan.AccessibleFacilities.Add(facility);
                    }
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
            }
            catch (Exception)
            {
                await transaction.RollbackAsync();
                throw;
            }
        }
    }
}
