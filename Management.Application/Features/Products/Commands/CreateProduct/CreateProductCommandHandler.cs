using Management.Application.DTOs;
using Management.Application.Interfaces;
using Management.Domain.Enums;
using Management.Domain.Interfaces;
using Management.Domain.Models;
using Management.Domain.Primitives;
using Management.Domain.ValueObjects;
using MediatR;
using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace Management.Application.Features.Products.Commands.CreateProduct
{
    public class CreateProductCommandHandler : IRequestHandler<CreateProductCommand, Result<Guid>>
    {
        private readonly IProductRepository _productRepository;
        private readonly Domain.Services.ITenantService _tenantService;
        private readonly ICurrentUserService _currentUserService;
        private readonly IUnitOfWork _unitOfWork;

        public CreateProductCommandHandler(
            IProductRepository productRepository, 
            Domain.Services.ITenantService tenantService,
            ICurrentUserService currentUserService,
            IUnitOfWork unitOfWork)
        {
            _productRepository = productRepository;
            _tenantService = tenantService;
            _currentUserService = currentUserService;
            _unitOfWork = unitOfWork;
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
                dto.Name ?? "Unnamed Product",
                dto.Description ?? string.Empty,
                price,
                cost,
                dto.StockQuantity,
                dto.SKU ?? string.Empty,
                category,
                dto.ImageUrl ?? string.Empty,
                dto.ReorderLevel);

            if (productResult.IsFailure)
            {
                return Result.Failure<Guid>(productResult.Error);
            }

            var product = productResult.Value;

            // 1. Mandatory Multi-Tenancy IDs
            var tenantId = _tenantService.GetTenantId();
            if (tenantId.HasValue) product.TenantId = tenantId.Value;

            // 2. Strict Facility ID Assignment
            // We MUST have a facility ID, otherwise the product is invisible in the Shop grid.
            var facilityId = request.FacilityId ?? _currentUserService.CurrentFacilityId;
            if (facilityId.HasValue && facilityId != Guid.Empty)
            {
                product.FacilityId = facilityId.Value;
            }
            else
            {
                // Last ditch effort: if we still have Guid.Empty, this product will be orphaned.
                // We should log this clearly as a Critical warning.
                Debug.WriteLine($"[CreateProduct] ❌ CRITICAL: No FacilityId found in request or context! Product will be saved with Guid.Empty.");
            }

            // 3. Ensure Active State
            product.Activate();

            // 4. Attach to Repository first
            await _productRepository.AddAsync(product, saveChanges: false);

            try
            {
                // DIAGNOSTIC LOGGING: State before save
                Debug.WriteLine($"[CreateProduct] PRE-SAVE: Name='{product.Name}' SKU='{product.SKU}' FacilityId='{product.FacilityId}'");

                int rowsAffected = await _unitOfWork.SaveChangesAsync(cancellationToken);

                Debug.WriteLine($"[CreateProduct] SUCCESS: {rowsAffected} row(s) saved.");
                return Result.Success(product.Id);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[CreateProduct] CRITICAL FAILURE: {ex.GetType().Name} - {ex.Message}");
                if (ex.InnerException != null)
                {
                    Debug.WriteLine($"  ↳ Inner: {ex.InnerException.Message}");
                }
                
                // MediatR pipeline might throw ValidationException from behaviors
                if (ex.Message.Contains("Validation failed"))
                {
                    return Result.Failure<Guid>(new Error("Product.Validation", ex.Message));
                }

                throw;
            }
        }
    }
}
