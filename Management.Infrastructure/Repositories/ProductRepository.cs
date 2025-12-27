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

        public async Task<IEnumerable<Product>> SearchAsync(string searchTerm, ProductCategory? category = null)
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
    }
}