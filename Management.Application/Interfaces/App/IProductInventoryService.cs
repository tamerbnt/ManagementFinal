using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Management.Application.DTOs;
using Management.Domain.Primitives;
using Management.Domain.ValueObjects;

namespace Management.Application.Interfaces.App
{
    public interface IProductInventoryService
    {
        Task<Result> LogRestockAsync(Guid facilityId, Guid productId, int quantity, Money unitCost, Money? newSalePrice, string notes);
        Task<Result<List<ProductInventoryTransactionDto>>> GetInventoryHistoryAsync(Guid facilityId, int? limit = null);
        Task<Result<ProductInventoryAnalyticsDto>> GetInventoryAnalyticsAsync(Guid facilityId);
    }

    public record ProductInventoryTransactionDto(
        Guid Id,
        string ProductName,
        string SKU,
        string Type,
        int QuantityChange,
        int ResultingStock,
        decimal UnitCost,
        decimal TotalCost,
        decimal? NewSalePrice,
        string Notes,
        DateTime Timestamp);

    public record CriticalStockForecasterDto(
        string ProductName,
        int CurrentStock,
        int DailyVelocity,
        int RunwayDays);

    public record ProductInventoryAnalyticsDto(
        string TopPurchasedItemName,
        decimal TopPurchasedItemTotalCost,
        string HighestVelocityItemName,
        int HighestVelocityItemQuantity,
        int LowStockCount,
        List<ProductInventoryTransactionDto> RecentTransactions,
        CriticalStockForecasterDto CriticalStock);
}
