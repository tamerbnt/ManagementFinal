using System;
using System.Threading;
using System.Threading.Tasks;
using Management.Application.Interfaces.App;
using Management.Application.Stores;
using Management.Domain.Interfaces;
using Management.Domain.Models;
using Management.Domain.Models.Enums;
using Management.Domain.Primitives;
using Management.Domain.ValueObjects;
using Management.Domain.Services;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Management.Application.Features.Inventory.Commands.LogRestock
{
    public record LogProductRestockCommand(
        Guid FacilityId,
        Guid ProductId,
        int Quantity,
        Money UnitCost,
        Money? NewSalePrice,
        string Notes) : IRequest<Result>;

    public class LogProductRestockCommandHandler : IRequestHandler<LogProductRestockCommand, Result>
    {
        private readonly IProductRepository _productRepository;
        private readonly IInventoryRepository _inventoryRepository;
        private readonly IUnitOfWork _unitOfWork;
        private readonly ProductStore _productStore;
        private readonly ITenantService _tenantService;
        private readonly ILogger<LogProductRestockCommandHandler> _logger;

        public LogProductRestockCommandHandler(
            IProductRepository productRepository,
            IInventoryRepository inventoryRepository,
            IUnitOfWork unitOfWork,
            ProductStore productStore,
            ITenantService tenantService,
            ILogger<LogProductRestockCommandHandler> logger)
        {
            _productRepository = productRepository;
            _inventoryRepository = inventoryRepository;
            _unitOfWork = unitOfWork;
            _productStore = productStore;
            _tenantService = tenantService;
            _logger = logger;
        }

        public async Task<Result> Handle(LogProductRestockCommand request, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Logging restock for product {ProductId} in facility {FacilityId}", request.ProductId, request.FacilityId);

            var product = await _productRepository.GetByIdAsync(request.ProductId, request.FacilityId);
            if (product == null)
            {
                return Result.Failure(new Error("Product.NotFound", $"Product {request.ProductId} not found."));
            }

            // 1. Update Product (Encapsulated WAC & Price logic)
            var updateResult = product.ReceiveStock(request.Quantity, request.UnitCost, request.NewSalePrice);
            if (updateResult.IsFailure) return updateResult;

            // 2. Create Transaction Log
            var transactionResult = InventoryTransaction.Create(
                _tenantService.GetTenantId() ?? Guid.Empty,
                request.FacilityId,
                request.ProductId,
                InventoryTransactionType.Purchase,
                request.Quantity,
                product.StockQuantity,
                request.UnitCost,
                request.NewSalePrice,
                request.Notes);

            if (transactionResult.IsFailure) return Result.Failure(transactionResult.Error);

            try
            {
                // 3. Persist Atomically
                await _productRepository.UpdateAsync(product);
                await _inventoryRepository.AddTransactionAsync(transactionResult.Value);
                await _unitOfWork.SaveChangesAsync(cancellationToken);

                // 4. Notify UI via ProductStore
                _productStore.TriggerStockUpdated(new DTOs.ProductDto
                {
                    Id = product.Id,
                    Name = product.Name,
                    StockQuantity = product.StockQuantity,
                    Price = product.Price.Amount,
                    Cost = product.Cost.Amount,
                    SKU = product.SKU
                });

                return Result.Success();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to log restock for product {ProductId}", request.ProductId);
                return Result.Failure(new Error("Inventory.SaveFailed", "Failed to save inventory transaction."));
            }
        }
    }
}
