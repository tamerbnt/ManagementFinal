using Management.Application.Stores;
using Management.Application.DTOs;
using Management.Domain.Enums;
using Management.Domain.Interfaces;
using Management.Domain.Models;
using Management.Domain.Primitives;
using Management.Domain.ValueObjects;
using MediatR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Management.Application.Interfaces;
using Management.Domain.Services;

namespace Management.Application.Features.Sales.Commands.ProcessCheckout
{
    public class ProcessCheckoutCommandHandler : IRequestHandler<ProcessCheckoutCommand, Result<bool>>
    {
        private readonly ISaleRepository _saleRepository;
        private readonly IProductRepository _productRepository;
        private readonly ProductStore _productStore;
        private readonly IMediator _mediator;
        private readonly ICurrentUserService _currentUserService;
        private readonly ITenantService _tenantService;
        private readonly IUnitOfWork _unitOfWork;

        public ProcessCheckoutCommandHandler(
            ISaleRepository saleRepository,
            IProductRepository productRepository,
            ProductStore productStore,
            IMediator mediator,
            ICurrentUserService currentUserService,
            ITenantService tenantService,
            IUnitOfWork unitOfWork)
        {
            _saleRepository = saleRepository;
            _productRepository = productRepository;
            _productStore = productStore;
            _mediator = mediator;
            _currentUserService = currentUserService;
            _tenantService = tenantService;
            _unitOfWork = unitOfWork;
        }

        public async Task<Result<bool>> Handle(ProcessCheckoutCommand request, CancellationToken cancellationToken)
        {
            var checkoutRequest = request.Request;
            if (checkoutRequest.Items == null || !checkoutRequest.Items.Any())
            {
                 return Result.Failure<bool>(new Error("Checkout.Empty", "Basket is empty."));
            }

            var paymentMethod = checkoutRequest.Method;

            // Determine Transaction Type
            string transactionType = "Retail"; // Default for shop checkouts
            
            var firstItem = checkoutRequest.Items.FirstOrDefault();
            var firstProduct = firstItem.Key != Guid.Empty ? await _productRepository.GetByIdAsync(firstItem.Key, request.FacilityId) : null;
            string? productName = firstProduct?.Name;

            // Senior Refactor: Products are always retail. Plans/memberships are sold
            // exclusively via Member Registration, never through the Shop/POS checkout.
            // Therefore, SaleCategory.Product is always correct here.
            SaleCategory category = SaleCategory.Product;
            string capturedLabel = productName ?? "Miscellaneous Product";

            var sale = Sale.Create(checkoutRequest.MemberId, paymentMethod, transactionType, category, capturedLabel);
            if (sale.IsFailure) return Result.Failure<bool>(sale.Error);

            var saleEntity = sale.Value;
            saleEntity.FacilityId = request.FacilityId;
            saleEntity.TenantId = _tenantService.GetTenantId() ?? Guid.Empty;

            await using var transaction = await _unitOfWork.BeginTransactionAsync(cancellationToken);
            try
            {
                var productsToUpdate = new List<Product>();

                foreach (var item in checkoutRequest.Items)
                {
                    var productId = item.Key;
                    var qty = item.Value;

                    var product = await _productRepository.GetByIdAsync(item.Key, request.FacilityId);
                    if (product == null)
                    {
                         return Result.Failure<bool>(new Error("Checkout.ProductNotFound", $"Product {item.Key} not found."));
                    }

                    if (product.StockQuantity < item.Value)
                    {
                         return Result.Failure<bool>(new Error("Checkout.InsufficientStock", $"Insufficient stock for {product.Name}. Available: {product.StockQuantity}"));
                    }

                    product.UpdateStock(-qty, "Sale");
                    productsToUpdate.Add(product);
                    
                    saleEntity.AddLineItem(product, qty);
                }

                await _saleRepository.AddAsync(saleEntity, saveChanges: false);

                foreach (var p in productsToUpdate)
                {
                    await _productRepository.UpdateAsync(p, saveChanges: false);
                }

                // FIX: Explicitly call SaveChangesAsync on the shared UnitOfWork context
                await _unitOfWork.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);

                // Notify UI after successful commit to prevent UI refresh storm during open transaction
                foreach (var p in productsToUpdate)
                {
                    _productStore.TriggerStockUpdated(new ProductDto 
                    { 
                        Id = p.Id, 
                        StockQuantity = p.StockQuantity,
                        Name = p.Name,
                        Price = p.Price.Amount,
                        SKU = p.SKU
                    });
                }

                // Notify activity stream
                var firstItemName = productsToUpdate.FirstOrDefault()?.Name ?? "Items";
                await _mediator.Publish(new Notifications.FacilityActionCompletedNotification(
                    request.FacilityId,
                    "Sale",
                    firstItemName,
                    $"Processed checkout for {productsToUpdate.Count} items"), cancellationToken);

                return Result.Success(true);
            }
            catch (Exception ex)
            {
                if (transaction != null) await transaction.RollbackAsync(cancellationToken);
                return Result.Failure<bool>(new Error("Checkout.DatabaseError", $"Failed to save sale: {ex.Message}"));
            }
        }
    }
}
