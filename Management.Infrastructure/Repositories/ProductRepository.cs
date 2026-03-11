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
    public class ProductRepository : Repository<Product>, IProductRepository
    {
        public ProductRepository(AppDbContext context) : base(context) { }

        public async Task<IEnumerable<Product>> SearchProductsAsync(string searchTerm, ProductCategory? category = null, Guid? facilityId = null)
        {
            var query = facilityId.HasValue 
                ? _dbSet.AsNoTracking().IgnoreQueryFilters().Where(p => p.FacilityId == facilityId.Value && p.IsActive)
                : _dbSet.AsNoTracking().Where(p => p.IsActive);

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                string term = searchTerm.ToLower();
                query = query.Where(p =>
                    p.Name.ToLower().Contains(term) ||
                    p.SKU.ToLower().Contains(term));
            }

            if (category.HasValue)
            {
                query = query.Where(p => p.Category == category.Value);
            }

            return await query.OrderBy(p => p.Name).ToListAsync();
        }

        public override async Task<Product?> GetByIdAsync(Guid id, Guid? facilityId = null)
        {
            if (facilityId.HasValue)
            {
                return await _dbSet.IgnoreQueryFilters()
                    .FirstOrDefaultAsync(p => p.Id == id && p.FacilityId == facilityId.Value);
            }
            return await base.GetByIdAsync(id);
        }

        public async Task<IEnumerable<Product>> GetLowStockAsync(Guid? facilityId = null)
        {
            var query = facilityId.HasValue
                ? _dbSet.IgnoreQueryFilters().Where(p => p.FacilityId == facilityId.Value && p.IsActive && p.StockQuantity <= p.ReorderLevel)
                : _dbSet.AsNoTracking().Where(p => p.IsActive && p.StockQuantity <= p.ReorderLevel);

            return await query
                .OrderBy(p => p.StockQuantity)
                .ToListAsync();
        }

        public async Task<IEnumerable<Product>> GetActiveProductsAsync(Guid? facilityId = null)
        {
            var query = facilityId.HasValue
                ? _dbSet.IgnoreQueryFilters().Where(p => p.FacilityId == facilityId.Value && p.IsActive)
                : _dbSet.AsNoTracking().Where(p => p.IsActive);

            return await query
                .OrderBy(p => p.Name)
                .ToListAsync();
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
    }
}
