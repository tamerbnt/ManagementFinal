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
        public RegistrationRepository(AppDbContext context) : base(context) { }

        public async Task<IEnumerable<Registration>> GetPendingRegistrationsAsync(System.Guid? facilityId = null)
        {
            var query = facilityId.HasValue
                ? _dbSet.IgnoreQueryFilters().Where(r => r.FacilityId == facilityId.Value && r.Status == RegistrationStatus.Pending && !r.IsDeleted)
                : _dbSet.AsNoTracking().Where(r => r.Status == RegistrationStatus.Pending);

            return await query
                .OrderByDescending(r => r.CreatedAt) // Newest first
                .ToListAsync();
        }

        public async Task<int> GetCountByStatusAsync(RegistrationStatus status, System.Guid? facilityId = null)
        {
            var query = facilityId.HasValue
                ? _dbSet.IgnoreQueryFilters().Where(r => r.FacilityId == facilityId.Value && r.Status == status && !r.IsDeleted)
                : _dbSet.Where(r => r.Status == status);

            return await query.CountAsync();
        }

        public override async Task<Registration?> GetByIdAsync(System.Guid id, System.Guid? facilityId = null)
        {
            if (facilityId.HasValue)
            {
                return await _dbSet.IgnoreQueryFilters()
                    .FirstOrDefaultAsync(p => p.Id == id && p.FacilityId == facilityId.Value && !p.IsDeleted);
            }
            return await base.GetByIdAsync(id);
        }

        public async Task<(IEnumerable<Registration> Items, int TotalCount)> SearchPagedAsync(
            string searchTerm,
            System.Guid? facilityId,
            int page,
            int pageSize,
            RegistrationStatus? status = null,
            RegistrationFilterType? filterType = null)
        {
            var query = _dbSet.AsNoTracking().Where(r => !r.IsDeleted);

            if (facilityId.HasValue)
            {
                query = _dbSet.IgnoreQueryFilters().Where(r => r.FacilityId == facilityId.Value && !r.IsDeleted);
            }

            if (status.HasValue)
            {
                query = query.Where(r => r.Status == status.Value);
            }

            if (filterType.HasValue)
            {
                if (filterType == RegistrationFilterType.New)
                {
                    var threshold = System.DateTime.UtcNow.AddDays(-1);
                    query = query.Where(r => r.CreatedAt >= threshold);
                }
                else if (filterType == RegistrationFilterType.Priority)
                {
                    query = query.Where(r => r.Source == "Walk-in" || r.Source == "Referral");
                }
            }

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                var lowerTerm = searchTerm.ToLower();
                query = query.Where(r => 
                    r.FullName.ToLower().Contains(lowerTerm) ||
                    (r.Email != null && r.Email.Value.ToLower().Contains(lowerTerm)) ||
                    (r.PhoneNumber != null && r.PhoneNumber.Value.ToLower().Contains(lowerTerm))
                );
            }

            var totalCount = await query.CountAsync();

            var items = await query
                .OrderByDescending(r => r.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return (items, totalCount);
        }

        public override async Task RestoreAsync(Guid id, Guid? facilityId = null)
        {
            var registration = await _dbSet.IgnoreQueryFilters().FirstOrDefaultAsync(r => r.Id == id);
            if (registration != null)
            {
                registration.Restore();
                await _context.SaveChangesAsync();
            }
        }
    }
}
