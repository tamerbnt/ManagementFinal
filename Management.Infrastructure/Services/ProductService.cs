using Management.Application.Features.Products.Queries.GetProducts;
using Management.Application.Services;
using Management.Application.Features.Products.Commands.CreateProduct;
using Management.Application.Services;
using Management.Application.Features.Products.Commands.UpdateProduct;
using Management.Application.Services;
using Management.Application.Features.Products.Commands.DeleteProduct;
using Management.Application.Services;
using Management.Application.Features.Products.Commands.UpdateProductStock;
using Management.Application.Services;
using Management.Application.DTOs;
using Management.Application.Services;
using Management.Domain.Enums;
using Management.Application.Services;
using Management.Domain.Primitives;
using Management.Application.Services;
using Management.Domain.Services;
using Management.Application.Services;
using MediatR;
using Management.Application.Services;
using System;
using Management.Application.Services;
using System.Collections.Generic;
using Management.Application.Services;
using System.Linq;
using Management.Application.Services;
using System.Threading.Tasks;
using Management.Application.Services;

namespace Management.Infrastructure.Services
{
    public class ProductService : IProductService
    {
        private readonly ISender _sender;

        public ProductService(ISender sender)
        {
            _sender = sender;
        }

        public async Task<Result<List<ProductDto>>> GetActiveProductsAsync(Guid facilityId)
        {
            return await _sender.Send(new GetActiveProductsQuery());
        }

        public async Task<Result<ProductDto>> GetProductAsync(Guid facilityId, Guid id)
        {
            return await _sender.Send(new GetProductByIdQuery(id));
        }

        public async Task<Result<List<InventoryDto>>> GetInventoryStatusAsync(Guid facilityId)
        {
            var result = await _sender.Send(new GetInventoryStatusQuery());
            if (result.IsFailure) return Result.Failure<List<InventoryDto>>(result.Error);
            
            var list = result.Value.Select(p => new InventoryDto(
                p.Id,
                p.Name,
                p.SKU,
                p.ImageUrl,
                p.StockQuantity,
                10,
                DateTime.UtcNow
            )).ToList();

            return Result.Success(list);
        }

        public async Task<Result<List<ProductDto>>> SearchProductsAsync(Guid facilityId, string searchTerm, ProductCategory? category = null)
        {
            return await _sender.Send(new SearchProductsQuery(searchTerm));
        }

        public async Task<Result> CreateProductAsync(Guid facilityId, ProductDto product)
        {
            return await _sender.Send(new CreateProductCommand(product));
        }

        public async Task<Result> UpdateProductAsync(Guid facilityId, ProductDto product)
        {
            return await _sender.Send(new UpdateProductCommand(product));
        }

        public async Task<Result> UpdateStockAsync(Guid facilityId, Guid productId, int quantityChange, string reason)
        {
            return await _sender.Send(new UpdateProductStockCommand(productId, quantityChange, reason));
        }

        public async Task<Result> DeleteProductAsync(Guid facilityId, Guid id)
        {
            return await _sender.Send(new DeleteProductCommand(id));
        }
    }
}
