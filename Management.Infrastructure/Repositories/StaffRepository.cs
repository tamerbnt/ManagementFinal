using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Management.Domain.Models;
using Management.Domain.Interfaces;
using Management.Infrastructure.Data;
using Management.Domain.ValueObjects;
using Management.Domain.Enums;

namespace Management.Infrastructure.Repositories
{
    public class StaffRepository : Repository<StaffMember>, IStaffRepository
    {
        public StaffRepository(AppDbContext context) : base(context) { }

        /// <summary>
        /// Exposes the underlying DbContext for advanced operations like facility seeding.
        /// </summary>
        public DbContext GetContext() => _context;

        public async Task<StaffMember?> GetByEmailAsync(string email, Guid? facilityId = null)
        {
            if (string.IsNullOrWhiteSpace(email)) return null;
            
            var emailResult = Email.Create(email);
            if (emailResult.IsFailure) return null;

            // Case-Insensitive Lookup (Simplified for EF Translation)
            // The Email internal value is already lowercased via Email.Create.
            // Direct object comparison is translatable via the ValueConverter.
            var emailObj = emailResult.Value;
            
            var query = _dbSet.IgnoreQueryFilters().Where(s => !s.IsDeleted && s.Email == emailObj);
            
            if (facilityId.HasValue)
            {
                query = query.Where(s => s.FacilityId == facilityId.Value);
            }

            return await query.FirstOrDefaultAsync();
        }

        public async Task<StaffMember?> GetByEmailAndFacilityTypeAsync(string email, Management.Domain.Enums.FacilityType targetType)
        {
            if (string.IsNullOrWhiteSpace(email)) return null;
            
            var emailResult = Management.Domain.ValueObjects.Email.Create(email);
            if (emailResult.IsFailure) return null;

            var emailObj = emailResult.Value;
            var appContext = _context as AppDbContext;
            
            if (appContext == null) return null;

            return await _dbSet.IgnoreQueryFilters()
                .Join(appContext.Facilities.IgnoreQueryFilters(),
                      s => s.FacilityId,
                      f => f.Id,
                      (s, f) => new { Staff = s, Facility = f })
                .Where(x => !x.Staff.IsDeleted && x.Staff.Email == emailObj && x.Facility.Type == targetType)
                .Select(x => x.Staff)
                .FirstOrDefaultAsync();
        }

        /// <summary>
        /// Looks up the FacilityType for a given FacilityId from the local SQLite database.
        /// This avoids querying Supabase facilities which is blocked by RLS during Cloud Recovery.
        /// Returns null if the facility is not found locally.
        /// </summary>
        public async Task<FacilityType?> GetFacilityTypeByIdAsync(Guid facilityId)
        {
            var appContext = _context as AppDbContext;
            if (appContext == null) return null;

            var facility = await appContext.Facilities
                .IgnoreQueryFilters()
                .AsNoTracking()
                .FirstOrDefaultAsync(f => f.Id == facilityId && !f.IsDeleted);

            return facility?.Type;
        }

        public async Task<StaffMember?> GetByCardIdAsync(string cardId, Guid? facilityId = null)
        {
            if (string.IsNullOrWhiteSpace(cardId)) return null;
            
            var query = _dbSet.IgnoreQueryFilters().Where(s => !s.IsDeleted && s.CardId == cardId);
            
            if (facilityId.HasValue)
            {
                query = query.Where(s => s.FacilityId == facilityId.Value);
            }

            return await query.FirstOrDefaultAsync();
        }

        public async Task<IEnumerable<StaffMember>> GetAllActiveAsync(Guid? facilityId = null)
        {
            var query = facilityId.HasValue
                ? _dbSet.IgnoreQueryFilters().Where(s => s.FacilityId == facilityId.Value && !s.IsDeleted && s.IsActive)
                : _dbSet.AsNoTracking().Where(s => !s.IsDeleted && s.IsActive);

            return await query
                .OrderBy(s => s.FullName)
                .ToListAsync();
        }

        public async Task SafeAddAsync(StaffMember staff)
        {
            // Tracker Guard: Avoid "Another instance is already being tracked" in memory
            var tracked = _context.ChangeTracker.Entries<StaffMember>()
                .FirstOrDefault(e => e.Entity.Id == staff.Id);

            if (tracked != null)
            {
                Serilog.Log.Information($"[StaffRepository] Entity {staff.Id} is already tracked in memory. Skipping Add.");
                return;
            }

            // Database Guard: Avoid Primary Key collision with concurrent background sync
            // IgnoreQueryFilters is critical here to ensure we see the record even if context is stale
            var existsInDb = await _dbSet.IgnoreQueryFilters().AnyAsync(s => s.Id == staff.Id);
            if (existsInDb)
            {
                Serilog.Log.Information($"[StaffRepository] Entity {staff.Id} already exists in database (found via filter-ignore). Skipping Add.");
                return;
            }

            await AddAsync(staff);
        }

        public async Task UpdatePinAsync(Guid staffId, string hashedPin)
        {
            var staff = await _dbSet.IgnoreQueryFilters().FirstOrDefaultAsync(s => s.Id == staffId);
            if (staff != null)
            {
                staff.SetPinCode(hashedPin);
                await _context.SaveChangesAsync();
            }
        }

        public override async Task RestoreAsync(Guid id, Guid? facilityId = null)
        {
            await _dbSet
                .IgnoreQueryFilters()
                .Where(s => s.Id == id)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(x => x.IsDeleted, false)
                    .SetProperty(x => x.UpdatedAt, DateTime.UtcNow));
        }

        public async Task CleanCrossFacilityStaffAsync(IEnumerable<Guid> authorizedFacilityIds)
        {
            try
            {
                var authIds = authorizedFacilityIds.ToList();
                if (!authIds.Any()) return;

                var crossStaff = await _dbSet
                    .IgnoreQueryFilters()
                    .Where(s => !authIds.Contains(s.FacilityId) && 
                                s.Role != Management.Domain.Enums.StaffRole.Owner && // PROTECT OWNERS
                                !s.IsDeleted)
                    .ToListAsync();

                if (crossStaff.Any())
                {
                    Serilog.Log.Information("[Security] Purging {Count} unauthorized cross-facility staff records.", crossStaff.Count);
                    _dbSet.RemoveRange(crossStaff);
                    await _context.SaveChangesAsync();
                }
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, "[Security] Failed to purge unauthorized staff records.");
            }
        }
    }
}
