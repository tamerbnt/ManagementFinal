using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Management.Domain.Interfaces;
using Management.Domain.Models;
using Management.Domain.Enums;
using Management.Infrastructure.Data;

namespace Management.Infrastructure.Repositories
{
    public class ProductRepository : Repository<Product>, IProductRepository
    {
        public ProductRepository(AppDbContext context) : base(context) { }

        public async Task<IEnumerable<Product>> SearchProductsAsync(string searchTerm, ProductCategory? category = null, Guid? facilityId = null)
        {
            var query = BuildSearchQuery(searchTerm, facilityId, category);
            return await query
                .OrderBy(p => p.Name)
                .ToListAsync();
        }

        public async Task<(IEnumerable<Product> Items, int TotalCount)> SearchProductsPagedAsync(
            string searchTerm,
            int page,
            int pageSize,
            ProductCategory? category = null,
            Guid? facilityId = null)
        {
            var query = BuildSearchQuery(searchTerm, facilityId, category);
            var totalCount = await query.CountAsync();
            var items = await query
                .OrderBy(p => p.Name)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return (items, totalCount);
        }

        private IQueryable<Product> BuildSearchQuery(string searchTerm, Guid? facilityId, ProductCategory? category = null)
        {
            var query = facilityId.HasValue
                ? _dbSet.AsNoTracking().IgnoreQueryFilters().Where(p => p.FacilityId == facilityId.Value && !p.IsDeleted && p.IsActive)
                : _dbSet.AsNoTracking().Where(p => !p.IsDeleted && p.IsActive);

            if (category.HasValue)
            {
                query = query.Where(p => p.Category == category.Value);
            }

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                string pattern = $"%{searchTerm}%";
                query = query.Where(p => 
                    EF.Functions.Like(p.Name, pattern) ||
                    (p.SKU != null && EF.Functions.Like(p.SKU, pattern))
                );
            }

            return query;
        }

        public async Task<IEnumerable<Product>> GetLowStockAsync(Guid? facilityId = null)
        {
            var query = facilityId.HasValue
                ? _dbSet.AsNoTracking().IgnoreQueryFilters().Where(p => p.FacilityId == facilityId.Value && !p.IsDeleted && p.StockQuantity <= p.ReorderLevel)
                : _dbSet.AsNoTracking().Where(p => !p.IsDeleted && p.StockQuantity <= p.ReorderLevel);

            return await query.OrderBy(p => p.Name).ToListAsync();
        }

        public async Task<IEnumerable<Product>> GetActiveProductsAsync(Guid? facilityId = null)
        {
            var query = facilityId.HasValue
                ? _dbSet.AsNoTracking().IgnoreQueryFilters().Where(p => p.FacilityId == facilityId.Value && !p.IsDeleted && p.IsActive)
                : _dbSet.AsNoTracking().Where(p => !p.IsDeleted && p.IsActive);

            return await query.OrderBy(p => p.Name).ToListAsync();
        }

        public async Task<IEnumerable<Product>> GetInventoryStatusAsync(Guid? facilityId = null)
        {
             var query = facilityId.HasValue
                ? _dbSet.IgnoreQueryFilters().Where(p => p.FacilityId == facilityId.Value && p.IsActive)
                : _dbSet.AsNoTracking().Where(p => p.IsActive);

             return await query
                .OrderBy(p => p.Category)
                .ThenBy(p => p.Name)
                .ToListAsync();
        }

        public override async Task<Product?> GetByIdAsync(Guid id, Guid? facilityId = null)
        {
            // ISOLATION: Detach existing entry to ensure fresh reload in shared DbContext
            var tracked = _context.ChangeTracker.Entries<Product>().FirstOrDefault(e => e.Entity.Id == id);
            if (tracked != null) tracked.State = EntityState.Detached;

            if (facilityId.HasValue)
            {
                return await _dbSet.IgnoreQueryFilters()
                    .FirstOrDefaultAsync(p => p.Id == id && p.FacilityId == facilityId.Value);
            }
            return await base.GetByIdAsync(id);
        }

        public override async Task RestoreAsync(Guid id, Guid? facilityId = null)
        {
            // BEFORE: fetch + Restore() + Activate() + SaveChangesAsync
            //         Risk: stale change-tracker entry with IsDeleted=true causes conflict.
            // AFTER:  ExecuteUpdateAsync — direct SQL UPDATE, bypasses the change tracker entirely.
            var query = _dbSet.IgnoreQueryFilters()
                .Where(p => p.Id == id);

            if (facilityId.HasValue)
                query = query.Where(p => p.FacilityId == facilityId.Value);

            await query.ExecuteUpdateAsync(s => s
                .SetProperty(p => p.IsDeleted, false)
                .SetProperty(p => p.IsActive, true)
                .SetProperty(p => p.UpdatedAt, DateTime.UtcNow));

            // Sync the Change Tracker: If the entity was tracked as Modified/Deleted in the same session,
            // the tracker remains stale. A subsequent SaveChangesAsync() would overwrite the DB with 
            // the stale state.
            var tracked = _context.ChangeTracker.Entries<Product>()
                .FirstOrDefault(e => e.Entity.Id == id);

            if (tracked != null)
            {
                tracked.Entity.Restore();
                tracked.Entity.Activate();
                tracked.State = EntityState.Unchanged; // Reflect that it now matches the DB
            }
        }
    }
}
