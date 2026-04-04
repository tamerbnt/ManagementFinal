using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Management.Application.Features.Inventory.Commands.LogRestock;
using Management.Application.Interfaces.App;
using Management.Domain.Interfaces;
using Management.Domain.Models;
using Management.Domain.Primitives;
using Management.Domain.ValueObjects;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Management.Application.Services
{
    public class ProductInventoryService : IProductInventoryService
    {
        private readonly IMediator _mediator;
        private readonly IInventoryRepository _inventoryRepository;
        private readonly IProductRepository _productRepository;
        private readonly ILogger<ProductInventoryService> _logger;

        public ProductInventoryService(
            IMediator mediator,
            IInventoryRepository inventoryRepository,
            IProductRepository productRepository,
            ILogger<ProductInventoryService> logger)
        {
            _mediator = mediator;
            _inventoryRepository = inventoryRepository;
            _productRepository = productRepository;
            _logger = logger;
        }

        public async Task<Result> LogRestockAsync(Guid facilityId, Guid productId, int quantity, Money unitCost, Money? newSalePrice, string notes)
        {
            var command = new LogProductRestockCommand(facilityId, productId, quantity, unitCost, newSalePrice, notes);
            return await _mediator.Send(command);
        }

        public async Task<Result<List<ProductInventoryTransactionDto>>> GetInventoryHistoryAsync(Guid facilityId, int? limit = null)
        {
            try
            {
                var transactions = await _inventoryRepository.GetHistoryAsync(facilityId, limit);
                var productIds = transactions.Select(t => t.ProductId).Distinct().ToList();
                
                // For a more professional UI, we need the product details (Name, SKU)
                var products = new Dictionary<Guid, Product>();
                foreach (var id in productIds)
                {
                    var p = await _productRepository.GetByIdAsync(id, facilityId);
                    if (p != null) products[id] = p;
                }

                var dtos = transactions.Select(t => new ProductInventoryTransactionDto(
                    t.Id,
                    products.TryGetValue(t.ProductId, out var p) ? p.Name : "Unknown Product",
                    products.TryGetValue(t.ProductId, out var p2) ? p2.SKU : "",
                    t.TransactionType.ToString(),
                    t.QuantityChange,
                    t.ResultingStock,
                    t.UnitCost.Amount,
                    t.TotalCost.Amount,
                    t.NewSalePrice?.Amount,
                    t.Notes,
                    t.Timestamp
                )).ToList();

                return Result.Success(dtos);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching inventory history for facility {FacilityId}", facilityId);
                return Result.Failure<List<ProductInventoryTransactionDto>>(new Error("Inventory.HistoryError", "Failed to fetch inventory history."));
            }
        }

        public async Task<Result<ProductInventoryAnalyticsDto>> GetInventoryAnalyticsAsync(Guid facilityId)
        {
            try
            {
                // Simple version for MVP: Last 30 transactions
                var historyResult = await GetInventoryHistoryAsync(facilityId, 100);
                if (historyResult.IsFailure) return Result.Failure<ProductInventoryAnalyticsDto>(historyResult.Error);

                var history = historyResult.Value;

                // 1. Top Purchased Item (By Money)
                var topMoney = history
                    .Where(t => t.Type == "Purchase")
                    .GroupBy(t => t.ProductName)
                    .Select(g => new { Name = g.Key, Total = g.Sum(x => x.TotalCost) })
                    .OrderByDescending(x => x.Total)
                    .FirstOrDefault();

                // 2. Highest Velocity (By Volume - Purchases)
                var topVolume = history
                    .Where(t => t.Type == "Purchase")
                    .GroupBy(t => t.ProductName)
                    .Select(g => new { Name = g.Key, Qty = g.Sum(x => x.QuantityChange) })
                    .OrderByDescending(x => x.Qty)
                    .FirstOrDefault();

                // 3. Low Stock Count
                var allProducts = await _productRepository.GetActiveProductsAsync(facilityId);
                int lowStock = allProducts.Count(p => p.IsActive && p.StockQuantity <= p.ReorderLevel);

                // 4. Critical Stock Forecaster (Smart Runway Engine)
                var thirtyDaysAgo = DateTime.UtcNow.AddDays(-30);
                var recentSales = history.Where(t => t.QuantityChange < 0 && t.Timestamp >= thirtyDaysAgo).ToList();
                
                CriticalStockForecasterDto criticalStock = new CriticalStockForecasterDto("All Healthy", 0, 0, int.MaxValue);

                if (allProducts.Any())
                {
                    var productVelocities = new List<CriticalStockForecasterDto>();
                    foreach (var product in allProducts.Where(p => p.IsActive && p.StockQuantity >= 0))
                    {
                        var productSales = recentSales.Where(s => s.ProductName == product.Name).Select(s => Math.Abs(s.QuantityChange)).ToList();
                        
                        // Spike filtering: skip the highest 1-day bulk anomaly if there are many transactions
                        if (productSales.Count > 3)
                        {
                            productSales.Remove(productSales.Max());
                        }

                        double consumed = productSales.Sum();
                        int dailyVelocity = (int)Math.Ceiling(consumed / 30.0);
                        int runway = dailyVelocity > 0 ? (product.StockQuantity / dailyVelocity) : int.MaxValue;
                        
                        productVelocities.Add(new CriticalStockForecasterDto(product.Name, product.StockQuantity, dailyVelocity, runway));
                    }

                    if (productVelocities.Any())
                    {
                        criticalStock = productVelocities.OrderBy(p => p.RunwayDays).First();
                    }
                }

                return Result.Success(new ProductInventoryAnalyticsDto(
                    topMoney?.Name ?? "N/A",
                    topMoney?.Total ?? 0,
                    topVolume?.Name ?? "N/A",
                    topVolume?.Qty ?? 0,
                    lowStock,
                    history.Take(10).ToList(),
                    criticalStock
                ));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating inventory analytics for facility {FacilityId}", facilityId);
                return Result.Failure<ProductInventoryAnalyticsDto>(new Error("Inventory.AnalyticsError", "Failed to generate inventory analytics."));
            }
        }
    }
}
