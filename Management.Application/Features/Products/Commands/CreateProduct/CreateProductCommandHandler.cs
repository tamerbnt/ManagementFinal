using Management.Application.DTOs;
using Management.Application.Interfaces;
using Management.Domain.Enums;
using Management.Domain.Interfaces;
using Management.Domain.Models;
using Management.Domain.Primitives;
using Management.Domain.ValueObjects;
using MediatR;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Management.Application.Features.Products.Commands.CreateProduct
{
    public class CreateProductCommandHandler : IRequestHandler<CreateProductCommand, Result<Guid>>
    {
        private readonly IProductRepository _productRepository;
        private readonly Domain.Services.ITenantService _tenantService;
        private readonly ICurrentUserService _currentUserService;

        public CreateProductCommandHandler(
            IProductRepository productRepository, 
            Domain.Services.ITenantService tenantService,
            ICurrentUserService currentUserService)
        {
            _productRepository = productRepository;
            _tenantService = tenantService;
            _currentUserService = currentUserService;
        }

        public async Task<Result<Guid>> Handle(CreateProductCommand request, CancellationToken cancellationToken)
        {
            var dto = request.Product;

            // Parse Category (String to Enum)
            if (!Enum.TryParse<ProductCategory>(dto.Category, true, out var category))
            {
                category = ProductCategory.Other; // Fallback
            }

            var price = new Money(dto.Price, dto.Currency ?? "DA");
            var cost = new Money(dto.Cost, dto.Currency ?? "DA");

            var productResult = Product.Create(
                dto.Name,
                dto.Description,
                price,
                cost,
                dto.StockQuantity,
                dto.SKU,
                category,
                dto.ImageUrl,
                dto.ReorderLevel);

            if (productResult.IsFailure)
            {
                return Result.Failure<Guid>(productResult.Error);
            }

            var product = productResult.Value;

            // Set multi-tenancy IDs
            var tenantId = _tenantService.GetTenantId();
            if (tenantId.HasValue) product.TenantId = tenantId.Value;

            var facilityId = request.FacilityId ?? _currentUserService.CurrentFacilityId;
            if (facilityId.HasValue && facilityId != Guid.Empty) product.FacilityId = facilityId.Value;

            // Persist to Repository (Supabase via AppDbContext)
            await _productRepository.AddAsync(product);

            return Result.Success(product.Id);
        }
    }
}
