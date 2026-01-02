using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Management.Domain.DTOs;
using Management.Domain.Enums;
using Management.Domain.Primitives;

namespace Management.Domain.Services
{
    public interface IProductService
    {
        Task<Result<List<ProductDto>>> GetActiveProductsAsync(Guid facilityId);
        Task<Result<ProductDto>> GetProductAsync(Guid facilityId, Guid id);
        Task<Result<List<InventoryDto>>> GetInventoryStatusAsync(Guid facilityId);
        Task<Result<List<ProductDto>>> SearchProductsAsync(Guid facilityId, string searchTerm, ProductCategory? category = null);
        Task<Result> CreateProductAsync(Guid facilityId, ProductDto product);
        Task<Result> UpdateProductAsync(Guid facilityId, ProductDto product);
        Task<Result> UpdateStockAsync(Guid facilityId, Guid productId, int quantityChange, string reason);
        Task<Result> DeleteProductAsync(Guid facilityId, Guid id);
    }
}
