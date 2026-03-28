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
using Microsoft.Extensions.Logging;

namespace Management.Application.Features.Sales.Commands.ProcessCheckout
{
    public class ProcessCheckoutCommandHandler : IRequestHandler<ProcessCheckoutCommand, Result<Guid>>
    {
        private readonly ISaleRepository _saleRepository;
        private readonly IProductRepository _productRepository;
        private readonly ProductStore _productStore;
        private readonly IMediator _mediator;
        private readonly ICurrentUserService _currentUserService;
        private readonly ITenantService _tenantService;
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<ProcessCheckoutCommandHandler> _logger;

        public ProcessCheckoutCommandHandler(
            ISaleRepository saleRepository,
            IProductRepository productRepository,
            ProductStore productStore,
            IMediator mediator,
            ICurrentUserService currentUserService,
            ITenantService tenantService,
            IUnitOfWork unitOfWork,
            ILogger<ProcessCheckoutCommandHandler> logger)
        {
            _saleRepository = saleRepository;
            _productRepository = productRepository;
            _productStore = productStore;
            _mediator = mediator;
            _currentUserService = currentUserService;
            _tenantService = tenantService;
            _unitOfWork = unitOfWork;
            _logger = logger;
        }

        public async Task<Result<Guid>> Handle(ProcessCheckoutCommand request, CancellationToken cancellationToken)
        {
            // ISOLATION: Clear the ChangeTracker to ensure zero interference from previous shared contexts.
            // This is the definitive fix for long-lived WPF DbContext state issues.
            _unitOfWork.ClearTracker();

            System.Diagnostics.Debug.WriteLine("[CHECKOUT] Handle started");
            var checkoutRequest = request.Request;
            if (checkoutRequest.Items == null || !checkoutRequest.Items.Any())
            {
                 return Result.Failure<Guid>(new Error("Checkout.Empty", "Basket is empty."));
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
            if (sale.IsFailure) return Result.Failure<Guid>(sale.Error);

            var saleEntity = sale.Value;
            saleEntity.FacilityId = request.FacilityId;
            saleEntity.TenantId = _tenantService.GetTenantId() ?? Guid.Empty;

            System.Diagnostics.Debug.WriteLine("[CHECKOUT] Beginning transaction");
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
                         return Result.Failure<Guid>(new Error("Checkout.ProductNotFound", $"Product {item.Key} not found."));
                    }

                    var decrementResult = product.DecrementStock(qty);
                    if (decrementResult.IsFailure)
                    {
                        return Result.Failure<Guid>(decrementResult.Error);
                    }
                    
                    productsToUpdate.Add(product);
                    
                    var addResult = saleEntity.AddLineItem(product, qty);
                    if (addResult.IsFailure)
                    {
                        return Result.Failure<Guid>(addResult.Error);
                    }
                }

                await _saleRepository.AddAsync(saleEntity, saveChanges: false);

                foreach (var p in productsToUpdate)
                {
                    await _productRepository.UpdateAsync(p, saveChanges: false);
                }

                // Notify UI after successful commit to prevent UI refresh storm during open transaction
                foreach (var p in productsToUpdate)
                {
                    _productStore.TriggerStockUpdated(new ProductDto 
                    { 
                        Id = p.Id, 
                        StockQuantity = p.StockQuantity,
                        Name = p.Name,
                        Price = p.Price?.Amount ?? 0,
                        SKU = p.SKU
                    });
                }

                // Flush all changes to the DB before committing
                System.Diagnostics.Debug.WriteLine("[CHECKOUT] Calling SaveChangesAsync");
                await _unitOfWork.SaveChangesAsync(cancellationToken);
                System.Diagnostics.Debug.WriteLine("[CHECKOUT] SaveChangesAsync completed");

                // COMMIT TRANSACTION BEFORE PUBLISHING NOTIFICATIONS
                System.Diagnostics.Debug.WriteLine("[CHECKOUT] Calling CommitAsync");
                await transaction.CommitAsync(cancellationToken);
                System.Diagnostics.Debug.WriteLine("[CHECKOUT] CommitAsync completed — returning result");

                // Notify activity stream - OUTSIDE TRANSACTION
                // If notification fails (e.g. UI delay), the sale is already saved.
                // Notify activity stream - OUTSIDE TRANSACTION
                if (request.PublishNotification)
                {
                    var firstItemName = productsToUpdate.FirstOrDefault()?.Name ?? "Items";
                    try 
                    {
                        System.Diagnostics.Debug.WriteLine("[CHECKOUT] Publishing notification...");
                        await _mediator.Publish(new Notifications.FacilityActionCompletedNotification(
                            request.FacilityId,
                            "Sale",
                            firstItemName,
                            $"Processed checkout for {productsToUpdate.Count} items",
                            saleEntity.Id.ToString()), cancellationToken);
                        System.Diagnostics.Debug.WriteLine("[CHECKOUT] Notification completed");
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[CHECKOUT] Notification EXCEPTION: {ex}");
                        _logger.LogWarning(ex, "Notification publishing failed for sale {SaleId}, but transaction was successful.", saleEntity.Id);
                    }
                }

                return Result.Success(saleEntity.Id);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[CHECKOUT] EXCEPTION: {ex}");
                // Only rollback if the transaction was NOT already committed successfully.
                // Since IUnitOfWorkTransaction is a generic interface, we use a simple try-catch for safety.
                if (transaction != null)
                {
                    try { await transaction.RollbackAsync(cancellationToken); } catch { /* Ignore rollback failure if already committed */ }
                }
                _logger.LogError(ex, "Checkout failed for facility {FacilityId}", request.FacilityId);
                return Result.Failure<Guid>(new Error("Checkout.DatabaseError", $"Failed to save sale: {ex.Message}"));
            }
        }
    }
}
