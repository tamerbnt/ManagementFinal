using System;
using Management.Application.Services;
using System.Collections.Generic;
using System.Threading.Tasks;
using Management.Application.DTOs;
using Management.Domain.Enums;
using Management.Domain.Primitives;

namespace Management.Application.Services
{
    public interface IProductService
    {
        Task<Result<List<ProductDto>>> GetActiveProductsAsync(Guid facilityId);
        Task<Result<ProductDto>> GetProductAsync(Guid facilityId, Guid id);
        Task<Result<List<InventoryDto>>> GetInventoryStatusAsync(Guid facilityId);
        Task<Result<List<ProductDto>>> SearchProductsAsync(Guid facilityId, string searchTerm, ProductCategory? category = null);
        Task<Result<PagedResult<ProductDto>>> SearchProductsPagedAsync(Guid facilityId, string searchTerm, int page = 1, int pageSize = 20, ProductCategory? category = null);
        Task<Result> CreateProductAsync(Guid facilityId, ProductDto product);
        Task<Result> UpdateProductAsync(Guid facilityId, ProductDto product);
        Task<Result> UpdateStockAsync(Guid facilityId, Guid productId, int quantityChange, string reason);
        Task<Result> DeleteProductAsync(Guid facilityId, Guid id);
        Task<Result> RestoreProductAsync(Guid facilityId, Guid id);
    }
}
