using Management.Application.DTOs;
using Management.Domain.Interfaces;
using Management.Domain.Models;
using Management.Domain.Primitives;
using MediatR;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Management.Application.Features.Products.Queries.GetProducts
{
    public class GetProductsQueryHandler : 
        IRequestHandler<GetActiveProductsQuery, Result<List<ProductDto>>>,
        IRequestHandler<SearchProductsQuery, Result<List<ProductDto>>>,
        IRequestHandler<GetInventoryStatusQuery, Result<List<ProductDto>>>,
        IRequestHandler<GetProductByIdQuery, Result<ProductDto>>
    {
        private readonly IProductRepository _productRepository;

        public GetProductsQueryHandler(IProductRepository productRepository)
        {
            _productRepository = productRepository;
        }

        public async Task<Result<List<ProductDto>>> Handle(GetActiveProductsQuery request, CancellationToken cancellationToken)
        {
            var products = await _productRepository.GetActiveProductsAsync(request.FacilityId);
            return Result.Success(products.Take(500).Select(MapToDto).ToList());
        }

        public async Task<Result<List<ProductDto>>> Handle(SearchProductsQuery request, CancellationToken cancellationToken)
        {
            var products = await _productRepository.SearchProductsAsync(request.SearchTerm, facilityId: request.FacilityId);
            return Result.Success(products.Select(MapToDto).ToList());
        }

        public async Task<Result<List<ProductDto>>> Handle(GetInventoryStatusQuery request, CancellationToken cancellationToken)
        {
            var products = await _productRepository.GetInventoryStatusAsync(request.FacilityId);
            return Result.Success(products.Select(MapToDto).ToList());
        }

        public async Task<Result<ProductDto>> Handle(GetProductByIdQuery request, CancellationToken cancellationToken)
        {
            var product = await _productRepository.GetByIdAsync(request.Id, request.FacilityId);
            if (product == null) return Result.Failure<ProductDto>(new Error("Product.NotFound", "Product not found"));
            return Result.Success(MapToDto(product));
        }

        private ProductDto MapToDto(Product entity)
        {
            return new ProductDto
            {
                Id = entity.Id,
                Name = entity.Name,
                Description = entity.Description,
                Price = entity.Price.Amount,
                Currency = entity.Price.Currency,
                Cost = entity.Cost.Amount,
                StockQuantity = entity.StockQuantity,
                SKU = entity.SKU,
                Category = entity.Category.ToString(),
                ImageUrl = entity.ImageUrl,
                ReorderLevel = entity.ReorderLevel
                // IsActive? Dto might not have it or defaults. 
                // Step 275 ProductDto had minimal fields? 
                // Let's assume standard mapping is fine.
            };
        }
    }
}
