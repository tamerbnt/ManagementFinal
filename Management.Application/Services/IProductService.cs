using System;
using Management.Application.Services;
using System.Collections.Generic;
using Management.Application.Services;
using System.Threading.Tasks;
using Management.Application.Services;
using Management.Application.DTOs;
using Management.Application.Services;
using Management.Domain.Enums;
using Management.Application.Services;
using Management.Domain.Primitives;
using Management.Application.Services;

namespace Management.Application.Services
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
