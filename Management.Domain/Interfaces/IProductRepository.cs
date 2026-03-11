using System.Collections.Generic;
using System.Threading.Tasks;
using Management.Domain.Enums;
using Management.Domain.Models;
using System; // Added for Guid

namespace Management.Domain.Interfaces
{
    public interface IProductRepository : IRepository<Product>
    {
        // For Shop Grid (POS)
        Task<IEnumerable<Product>> SearchProductsAsync(string searchTerm, ProductCategory? category = null, Guid? facilityId = null);

        // For Inventory Alerts
        Task<IEnumerable<Product>> GetLowStockAsync(Guid? facilityId = null);

        Task<Product?> GetByIdAsync(Guid id, Guid? facilityId = null);

        // For Queries
        Task<IEnumerable<Product>> GetActiveProductsAsync(Guid? facilityId = null);
        Task<IEnumerable<Product>> GetInventoryStatusAsync(Guid? facilityId = null);
    }
}
