using System.Collections.Generic;
using System.Threading.Tasks;
using Management.Domain.Enums;
using Management.Domain.Models;

namespace Management.Domain.Interfaces
{
    public interface IProductRepository : IRepository<Product>
    {
        // For Shop Grid (POS)
        Task<IEnumerable<Product>> SearchAsync(string searchTerm, ProductCategory? category = null);

        // For Inventory Alerts
        Task<IEnumerable<Product>> GetLowStockAsync();
    }
}