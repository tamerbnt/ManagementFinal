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
        public ProductRepository(GymDbContext context) : base(context) { }

        public async Task<IEnumerable<Product>> SearchProductsAsync(string searchTerm, ProductCategory? category = null)
        {
            var query = _dbSet.AsNoTracking().Where(p => p.IsActive);

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

        public async Task<IEnumerable<Product>> GetLowStockAsync()
        {
            return await _dbSet.AsNoTracking()
                .Where(p => p.IsActive && p.StockQuantity <= p.ReorderLevel)
                .OrderBy(p => p.StockQuantity)
                .ToListAsync();
        }

        public async Task<IEnumerable<Product>> GetActiveProductsAsync()
        {
            return await _dbSet.AsNoTracking()
                .Where(p => p.IsActive)
                .OrderBy(p => p.Name)
                .ToListAsync();
        }

        public async Task<IEnumerable<Product>> GetInventoryStatusAsync()
        {
             // For Inventory Dashboard, return all active products sorted by stock level implicitly?
             // Or maybe even inactive ones if managing inventory?
             // Let's return Active ones for now, sorted by name.
             // Or better, sorted by StockQuantity to see what's low/high? LowStockAsync does that.
             // Let's sort by Category then Name.
             return await _dbSet.AsNoTracking()
                .Where(p => p.IsActive)
                .OrderBy(p => p.Category)
                .ThenBy(p => p.Name)
                .ToListAsync();
        }
    }
}