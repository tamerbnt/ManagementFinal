using System.Collections.Generic;
using System.Threading.Tasks;
using Management.Domain.Enums;
using Management.Domain.Models;

namespace Management.Domain.Interfaces
{
    public interface IProductRepository : IRepository<Product>
    {
        // For Shop Grid (POS)
        Task<IEnumerable<Product>> SearchProductsAsync(string searchTerm, ProductCategory? category = null);

        // For Inventory Alerts
        Task<IEnumerable<Product>> GetLowStockAsync();

        // For Queries
        Task<IEnumerable<Product>> GetActiveProductsAsync();
        Task<IEnumerable<Product>> GetInventoryStatusAsync();
    }
}