using Management.Application.DTOs;
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
using Management.Application.Stores;

namespace Management.Application.Features.Products.Commands.UpdateProduct
{
    public class UpdateProductCommandHandler : IRequestHandler<UpdateProductCommand, Result>
    {
        private readonly IProductRepository _productRepository;
        private readonly ProductStore _productStore;
        private readonly IUnitOfWork _unitOfWork;

        public UpdateProductCommandHandler(IProductRepository productRepository, ProductStore productStore, IUnitOfWork unitOfWork)
        {
            _productRepository = productRepository;
            _productStore = productStore;
            _unitOfWork = unitOfWork;
        }

        public async Task<Result> Handle(UpdateProductCommand request, CancellationToken cancellationToken)
        {
            var dto = request.Product;
            Debug.WriteLine($"[UpdateProduct] ► START  ProductId={dto.Id}  FacilityId={request.FacilityId}");

            // ISOLATION: Clear the ChangeTracker to ensure zero interference from previous shared contexts.
            _unitOfWork.ClearTracker();

            var product = await _productRepository.GetByIdAsync(dto.Id, request.FacilityId);

            if (product == null)
            {
                Debug.WriteLine($"[UpdateProduct] ✗ Product NOT found in DB. Check FacilityId match.");
                return Result.Failure(new Error("Product.NotFound", $"Product {dto.Id} not found."));
            }

            Debug.WriteLine($"[UpdateProduct] ✓ Found: Name='{product.Name}'  Price={product.Price?.Amount}  Stock={product.StockQuantity}");

            if (!Enum.TryParse<ProductCategory>(dto.Category, out var category))
                category = ProductCategory.Other;

            var price = new Money(dto.Price, dto.Currency ?? "DA");
            var cost  = new Money(dto.Cost,  dto.Currency ?? "DA");

            product.UpdateDetails(
                dto.Name,
                dto.Description,
                dto.SKU,
                price,
                cost,
                dto.ReorderLevel,
                category,
                dto.ImageUrl);

            int diff = dto.StockQuantity - product.StockQuantity;
            if (diff != 0)
                product.UpdateStock(diff, "Manual Adjustment via Edit");

            // Ensure the product is marked as Active so it appears in Shop filters
            product.Activate();

            Debug.WriteLine($"[UpdateProduct] → Prepared: Name='{product.Name}'  Price={product.Price?.Amount}  Stock={product.StockQuantity}");

            // Attach to repository 
            await _productRepository.UpdateAsync(product, saveChanges: false);

            // SYNC Shadow Property (AFTER attachment so EF is tracking it)
            _unitOfWork.SetShadowProperty(product, "price", product.Price?.Amount ?? 0);

            // DIAGNOSTICS: Check ChangeTracker state before saving.
            Debug.WriteLine("--- CHANGE TRACKER DEBUG VIEW ---");
            Debug.WriteLine(_unitOfWork.GetChangeTrackerDebugView());

            // PERSISTENCE: Save through UoW
            int rowsAffected = await _unitOfWork.SaveChangesAsync(cancellationToken);

            if (rowsAffected == 0)
            {
                Debug.WriteLine($"[UpdateProduct] ⚠ WARNING — SaveChanges returned 0 rows! ChangeTracker might have marked the entity as Unchanged if properties matched exactly.");
            }
            else
            {
                Debug.WriteLine($"[UpdateProduct] ✓ SaveChanges committed {rowsAffected} row(s) to SQLite.");
            }

            // Notify ViewModels (QuickSale, Shop, etc.) that the product changed.
            _productStore.TriggerStockUpdated(new ProductDto
            {
                Id          = product.Id,
                StockQuantity = product.StockQuantity,
                Name        = product.Name,
                Description = product.Description,
                Price       = product.Price?.Amount ?? 0,
                Cost        = product.Cost?.Amount  ?? 0,
                SKU         = product.SKU,
                Category    = product.Category.ToString(),
                ImageUrl    = product.ImageUrl,
                ReorderLevel = product.ReorderLevel
            });

            return Result.Success();
        }
    }
}
